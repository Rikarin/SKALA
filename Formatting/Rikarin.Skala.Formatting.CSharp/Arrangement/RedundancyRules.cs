using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// <c>this.field</c> ⇒ <c>field</c>, under <c>resharper_remove_this_qualifier</c>.
/// </summary>
public sealed class ThisQualifierRule : ArrangementRule {
    public override string Id => ArrangeIds.ThisQualifier;

    /// <summary>
    /// ⚠ Semantic, and it is worth saying why, because <c>this.x</c> ⇒ <c>x</c> looks like a string
    /// edit. Removing the qualifier changes the set of things the bare name can bind to: a local, a
    /// parameter, a static of the same name, a using-imported extension. The rewrite is only legal
    /// when the bare name binds to the same symbol, and that is a question only the model answers.
    /// It is also the reason this rule is on layer 3's list in doc 06 § "Safety".
    /// </summary>
    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) => options.RemoveThisQualifier;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (node.Expression is not ThisExpressionSyntax || !node.IsKind(SyntaxKind.SimpleMemberAccessExpression)) {
                return visited;
            }

            if (model.GetSymbolInfo(node).Symbol is not { } qualified) {
                return visited;
            }

            // ⚠ The precondition: the bare name, looked up at exactly this position, must find the
            // same symbol. `LookupSymbols` is asked rather than the syntax re-bound, because the
            // rewritten tree is not in the model and re-binding it would need a whole new
            // compilation — which is layer 2's job, not layer 1's.
            var candidates = model.LookupSymbols(node.SpanStart, name: node.Name.Identifier.ValueText);
            if (candidates.Length != 1 || !SymbolEqualityComparer.Default.Equals(candidates[0], qualified)) {
                return visited;
            }

            return visited.Name.WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }
    }
}

/// <summary>
/// <c>{ { x; } }</c> ⇒ <c>{ x; }</c>, under <c>resharper_braces_redundant</c>.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/06 § "Qualification and redundancy" resolves what looks like a contradiction in the
/// export — <c>csharp_prefer_braces = true</c> (Microsoft: always use braces) beside
/// <c>resharper_braces_redundant = true</c> (ReSharper: remove braces that add nothing). They govern
/// different things: this rule removes a *nested block that is a statement of another block*, and
/// never the braces of an <c>if</c>, a <c>while</c> or a <c>using</c>. Reading it the other way
/// turns "always brace your ifs" into "unbrace them all".
/// <para>
/// ⚠ A block that declares anything is not redundant: hoisting its declarations into the parent
/// changes their scope, and can collide with a name the parent already has. That is the whole
/// precondition and it is checked syntactically, which is why this rule is in the free subset.
/// </para>
/// </remarks>
public sealed class RedundantBracesRule : ArrangementRule {
    public override string Id => ArrangeIds.RedundantBraces;

    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) => options.BracesRedundant;

    public override SyntaxNode Apply(ArrangementContext context) => new Rewriter(context.Guard).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitBlock(BlockSyntax node) {
            var visited = (BlockSyntax)base.VisitBlock(node)!;
            var statements = new List<StatementSyntax>();
            var changed = false;

            foreach (var statement in visited.Statements) {
                if (statement is BlockSyntax inner && IsRedundant(inner)) {
                    // The inner block's own leading trivia belongs to its first statement now.
                    var first = true;
                    foreach (var lifted in inner.Statements) {
                        statements.Add(first ? lifted.WithLeadingTrivia(inner.GetLeadingTrivia()) : lifted);
                        first = false;
                    }

                    changed = true;
                    continue;
                }

                statements.Add(statement);
            }

            return changed ? visited.WithStatements(SyntaxFactory.List(statements)) : visited;
        }

        static bool IsRedundant(BlockSyntax block) {
            foreach (var statement in block.Statements) {
                // A declaration's scope is the block. Lifting it widens that scope.
                if (statement is LocalDeclarationStatementSyntax
                    or LocalFunctionStatementSyntax
                    or LabeledStatementSyntax) {
                    return false;
                }
            }

            // ⚠ A directive inside the braces may be what the braces are there for; a `#if` that
            // opens in one block and closes in another is exactly the shape ADR-003 refuses to move.
            foreach (var trivia in block.DescendantTrivia(descendIntoTrivia: true)) {
                if (trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia)) {
                    return false;
                }
            }

            return true;
        }
    }
}

/// <summary>
/// <c>a + (b * c)</c> ⇒ <c>a + b * c</c>. ⚠ Gated behind <c>--aggressive</c>.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/06: "Parenthesis removal is the highest-risk rewrite in the whole tool […] which is
/// correct and which people find alarming. […] Skala gates parenthesis removal behind
/// <c>arrange --aggressive</c> for the first release regardless, and revisits when the corpus
/// differential shows zero divergences." The oracle's cleanup profile *does* remove them, so the
/// gate is a measured divergence rather than a hidden one — the M4 report gives its cost in changed
/// spans, both ways.
/// </remarks>
public sealed class RedundantParenthesesRule : ArrangementRule {
    public override string Id => ArrangeIds.RedundantParentheses;

    public override bool NeedsSemantics => false;

    public override bool IsAggressive => true;

    public override bool IsEnabled(in ArrangementOptions options) => options.Aggressive;

    public override SyntaxNode Apply(ArrangementContext context) => new Rewriter(context.Guard).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node) {
            var visited = (ParenthesizedExpressionSyntax)base.VisitParenthesizedExpression(node)!;
            if (!IsRedundant(node)) {
                return visited;
            }

            return visited.Expression.WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        /// <summary>
        /// True only for the arithmetic case doc 06 names, and only when precedence alone settles it.
        /// </summary>
        /// <remarks>
        /// ⚠ <c>dotnet_style_parentheses_in_other_binary_operators = always_for_clarity</c>, so the
        /// relational, logical and bitwise families keep theirs — this rule is arithmetic only, and
        /// widening it is not a small change.
        /// </remarks>
        static bool IsRedundant(ParenthesizedExpressionSyntax node) {
            if (node.Expression is not BinaryExpressionSyntax inner
                || node.Parent is not BinaryExpressionSyntax outer) {
                return false;
            }

            if (!IsArithmetic(inner) || !IsArithmetic(outer)) {
                return false;
            }

            var innerPrecedence = Precedence(inner);
            var outerPrecedence = Precedence(outer);
            if (innerPrecedence > outerPrecedence) {
                return true;
            }

            // ⚠ Equal precedence is only safe on the left. `a - (b - c)` is not `a - b - c`, and
            // neither is the division case; the associativity that makes the left side safe is
            // exactly what makes the right side wrong.
            return innerPrecedence == outerPrecedence && outer.Left == node && IsAssociative(outer);
        }

        static bool IsArithmetic(BinaryExpressionSyntax expression) =>
            expression.Kind() is SyntaxKind.AddExpression
                or SyntaxKind.SubtractExpression
                or SyntaxKind.MultiplyExpression
                or SyntaxKind.DivideExpression
                or SyntaxKind.ModuloExpression;

        static int Precedence(BinaryExpressionSyntax expression) =>
            expression.Kind() switch {
                SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression => 2,
                _ => 1
            };

        /// <summary>
        /// ⚠ <c>+</c> on <c>string</c> and on floating point is not associative in the way this
        /// rewrite needs, but the *shape* <c>(a + b) + c</c> ⇒ <c>a + b + c</c> re-associates
        /// nothing: it is already left-to-right. Only <c>-</c> and <c>/</c> change meaning, and they
        /// are excluded by returning false.
        /// </summary>
        static bool IsAssociative(BinaryExpressionSyntax expression) =>
            expression.Kind() is SyntaxKind.AddExpression or SyntaxKind.MultiplyExpression;
    }
}
