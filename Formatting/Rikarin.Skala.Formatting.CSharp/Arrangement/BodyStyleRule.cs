using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     Block body ⇄ expression body, under <c>use_heuristics_for_body_style</c>.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/06 stated five conditions for the heuristic and one of them is wrong. Measured
///     against <c>jb cleanupcode</c> 2025.2.6 (the sweep is in <c>docs/oracle-cleanup-profile.md</c>),
///     the oracle converts:
///     <list type="bullet">
///         <item>
///             a single-statement body whose statement is an expression statement or a <c>return</c> with
///             a value — and <em>not</em> a <c>throw</c>, which stays a block;
///         </item>
///         <item>only when the body holds no comment;</item>
///         <item>only when the body holds no <c>#if</c>;</item>
///         <item>never for <c>async void</c>;</item>
///         <item>
///             ⚠ <b>regardless of how long the result is.</b> A 200-column body converts and is then
///             wrapped after the <c>=&gt;</c> by the reformat that follows. Doc 06's condition (c), "the
///             converted form fits <c>max_line_length</c> at the member's indentation", is not a condition the
///             oracle applies, and implementing it would refuse a conversion the oracle performs on every long
///             one-line method in the corpus.
///         </item>
///     </list>
///     The doc has been corrected; this comment is the measurement it was corrected from.
/// </remarks>
public sealed class BodyStyleRule : ArrangementRule {
    public override string Id => ArrangeIds.BodyStyle;

    /// <summary>
    ///     ⚠ Syntactic. Every condition above is a question about the tree — <c>async void</c> is two
    ///     tokens, "no comment" is trivia, and a <c>return</c> with a value is a node kind. This is one
    ///     of the rewrites an agent gets on a loose file with no project.
    /// </summary>
    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, ArrangementOptions options) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) {
            var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;

            // ⚠ Gated on the heuristic, because "never for `async void`" *is* one of the heuristics.
            // Measured on `body-style/heuristics.cs` at `use_heuristics_for_body_style = false`: the
            // oracle writes `public async void AsyncVoid() => await Task.Delay(1);`. Skala refused
            // unconditionally, which is right at the export's `true` and wrong at `false`.
            if (options.UseHeuristicsForBodyStyle && IsAsyncVoid(visited)) {
                return visited;
            }

            return Convert(
                visited,
                options.MethodOrOperatorBody,
                visited.Body,
                visited.ExpressionBody,
                static (member, arrow, semicolon) => member.WithBody(null)
                    .WithExpressionBody(arrow)
                    .WithSemicolonToken(semicolon),
                static (member, block) => member.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(block)
            );
        }

        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node) {
            var visited = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;
            return Convert(
                visited,
                options.MethodOrOperatorBody,
                visited.Body,
                visited.ExpressionBody,
                static (member, arrow, semicolon) => member.WithBody(null)
                    .WithExpressionBody(arrow)
                    .WithSemicolonToken(semicolon),
                static (member, block) => member.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(block)
            );
        }

        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) {
            var visited = (ConversionOperatorDeclarationSyntax)base.VisitConversionOperatorDeclaration(node)!;
            return Convert(
                visited,
                options.MethodOrOperatorBody,
                visited.Body,
                visited.ExpressionBody,
                static (member, arrow, semicolon) => member.WithBody(null)
                    .WithExpressionBody(arrow)
                    .WithSemicolonToken(semicolon),
                static (member, block) => member.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(block)
            );
        }

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) {
            var visited = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;

            // ⚠ The same heuristic and the same gate as VisitMethodDeclaration's.
            if (options.UseHeuristicsForBodyStyle
                && visited.Modifiers.Any(SyntaxKind.AsyncKeyword)
                && IsVoid(visited.ReturnType)) {
                return visited;
            }

            return Convert(
                visited,
                options.LocalFunctionBody,
                visited.Body,
                visited.ExpressionBody,
                static (member, arrow, semicolon) => member.WithBody(null)
                    .WithExpressionBody(arrow)
                    .WithSemicolonToken(semicolon),
                static (member, block) => member.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(block)
            );
        }

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) {
            var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
            return Convert(
                visited,
                options.ConstructorOrDestructorBody,
                visited.Body,
                visited.ExpressionBody,
                static (member, arrow, semicolon) => member.WithBody(null)
                    .WithExpressionBody(arrow)
                    .WithSemicolonToken(semicolon),
                static (member, block) => member.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(block)
            );
        }

        public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node) {
            var visited = (DestructorDeclarationSyntax)base.VisitDestructorDeclaration(node)!;
            return Convert(
                visited,
                options.ConstructorOrDestructorBody,
                visited.Body,
                visited.ExpressionBody,
                static (member, arrow, semicolon) => member.WithBody(null)
                    .WithExpressionBody(arrow)
                    .WithSemicolonToken(semicolon),
                static (member, block) => member.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(block)
            );
        }

        /// <summary>
        ///     ⚠ The owner rule, which is what <c>accessor_owner_body</c> actually means. Measured: a
        ///     property whose only accessor is a <c>get</c> collapses to an expression body on the
        ///     <em>property</em> (<c>public int P =&gt; _n;</c>); a property with more than one accessor
        ///     keeps its accessor list and each accessor gets an expression body
        ///     (<c>get =&gt; _n; set =&gt; _n = value;</c>). One key, two shapes, and reading it as
        ///     "expression bodies on accessors" loses the first.
        /// </summary>
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node) {
            var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
            if (options.AccessorOwnerBody != AccessorOwnerBodyStyle.ExpressionBody
                || visited.AccessorList is not { } accessors) {
                return visited;
            }

            if (accessors.Accessors is [{ } only]
                && only.IsKind(SyntaxKind.GetAccessorDeclaration)
                && only.AttributeLists.Count == 0
                && only.Modifiers.Count == 0
                && ExtractAccessor(only) is { } expression
                && !HasTriviaThatBlocksConversion(accessors)) {
                return visited.WithAccessorList(null)
                    .WithExpressionBody(Arrow(expression))
                    .WithSemicolonToken(Semicolon(accessors.CloseBraceToken));
            }

            return visited;
        }

        /// <summary>
        ///     An accessor with a single-statement block becomes <c>get =&gt; …;</c>.
        /// </summary>
        /// <remarks>
        ///     ⚠ Runs before <see cref="VisitPropertyDeclaration" /> sees the property, because
        ///     <c>base.Visit…</c> descends first. The owner collapse above therefore reads an accessor
        ///     list that may already carry expression bodies, which is why it calls
        ///     <see cref="Extract(BlockSyntax?)" /> on the block rather than on the accessor: an accessor
        ///     this method has already converted is handled by <see cref="ExtractAccessor" />.
        /// </remarks>
        public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node) {
            var visited = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;
            if (options.AccessorOwnerBody == AccessorOwnerBodyStyle.AccessorsWithBlockBody) {
                return visited;
            }

            if (visited.Body is not { } block
                || Extract(block, allowExpressionStatement: true) is not { } expression) {
                return visited;
            }

            return visited.WithBody(null)
                .WithExpressionBody(Arrow(expression))
                .WithSemicolonToken(Semicolon(block.CloseBraceToken));
        }

        /// <summary>⚠ An indexer is an accessor owner too, and reads the same key.</summary>
        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node) {
            var visited = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node)!;
            if (options.AccessorOwnerBody != AccessorOwnerBodyStyle.ExpressionBody
                || visited.AccessorList is not { } accessors) {
                return visited;
            }

            if (accessors.Accessors is [{ } only]
                && only.IsKind(SyntaxKind.GetAccessorDeclaration)
                && only.AttributeLists.Count == 0
                && only.Modifiers.Count == 0
                && ExtractAccessor(only) is { } expression
                && !HasTriviaThatBlocksConversion(accessors)) {
                return visited.WithAccessorList(null)
                    .WithExpressionBody(Arrow(expression))
                    .WithSemicolonToken(Semicolon(accessors.CloseBraceToken));
            }

            return visited;
        }

        /// <summary>The expression an already-converted accessor carries, for the owner collapse.</summary>
        static ExpressionSyntax? ExtractAccessor(AccessorDeclarationSyntax accessor) =>
            accessor.ExpressionBody?.Expression ?? Extract(accessor.Body);

        TMember Convert<TMember>(
            TMember member,
            BodyStyle style,
            BlockSyntax? body,
            ArrowExpressionClauseSyntax? expressionBody,
            Func<TMember, ArrowExpressionClauseSyntax, SyntaxToken, TMember> toExpression,
            Func<TMember, BlockSyntax, TMember> toBlock
        ) where TMember : SyntaxNode {
            // ⚠ A constructor has no return value, so its single statement is always an expression
            // statement; requiring a `return` would make `constructor_or_destructor_body =
            // expression_body` a setting that can never fire. And with the heuristic OFF, doc 06's
            // description applies literally — "*every* single-statement method becomes an expression
            // body" — so the `throw` and void-expression exclusions lift with it.
            var loose = member is ConstructorDeclarationSyntax or DestructorDeclarationSyntax
                || !options.UseHeuristicsForBodyStyle;

            if (style == BodyStyle.ExpressionBody) {
                if (body is null
                    || Extract(body, loose, allowThrow: !options.UseHeuristicsForBodyStyle)
                    is not { } expression) {
                    return member;
                }

                return toExpression(member, Arrow(expression), Semicolon(body.CloseBraceToken));
            }

            if (expressionBody is null) {
                return member;
            }

            return toBlock(member, BlockFor(member, expressionBody.Expression));
        }

        /// <summary>
        ///     The expression a block body can collapse to, or null when it may not collapse at all.
        /// </summary>
        /// <remarks>
        ///     ⚠ Every "return null" here is docs/plan/06 § "Safety" layer 1 in miniature: a body this
        ///     method does not understand stays a block. There is no "probably fine".
        /// </remarks>
        static ExpressionSyntax? Extract(
            BlockSyntax? body,
            bool allowExpressionStatement = false,
            bool allowThrow = false
        ) {
            if (body is null || body.Statements.Count != 1 || HasTriviaThatBlocksConversion(body)) {
                return null;
            }

            if (allowThrow && body.Statements[0] is ThrowStatementSyntax { Expression: { } thrown }) {
                return SyntaxFactory.ThrowExpression(
                    SyntaxFactory.Token(SyntaxKind.ThrowKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                    thrown.WithoutLeadingTrivia().WithTrailingTrivia()
                );
            }

            return body.Statements[0] switch {
                // ⚠ Only in an accessor. Measured: the oracle converts `set { _n = value; }` to
                // `set => _n = value;` and leaves `void Helper() { _shared = 1; }` a block — a
                // *method* body converts only when its statement is a `return` with a value, so a
                // void method is never a candidate however short it is. Doc 06 said "one statement
                // that is an expression, `return`, or `throw`", which is wrong in both directions:
                // `throw` never converts, and a bare expression converts only for an accessor.
                ExpressionStatementSyntax statement when allowExpressionStatement => statement.Expression,

                // ⚠ `return;` with no value has no expression to become one. `=> ;` is not a thing.
                ReturnStatementSyntax { Expression: { } value } => value,

                // ⚠ `throw` is excluded by measurement, not by taste: the oracle leaves
                // `{ throw new X(); }` a block, and `=> throw new X()` is legal C# it declines to
                // write. docs/plan/06 said so and the oracle agrees.
                _ => null
            };
        }

        /// <summary>
        ///     A comment or a directive inside the body blocks the conversion, because there is nowhere
        ///     for either to go: an expression body has no statement list to hold a line comment, and a
        ///     <c>#if</c> that straddles the only statement is not a single statement at all.
        /// </summary>
        /// <remarks>
        ///     ⚠ A comment blocks the conversion at <em>both</em> values of
        ///     <c>use_heuristics_for_body_style</c>, and at <c>false</c> that is a divergence rather than
        ///     the rule — SK-DIV-0084. The oracle converts there and writes the comment on its own line
        ///     between the <c>=&gt;</c> and the expression; carrying it through arrangement is easy and
        ///     the *formatter* then leaves it at column 0, which is worse than not converting. It is the
        ///     formatter's comment placement that has to move first.
        /// </remarks>
        static bool HasTriviaThatBlocksConversion(SyntaxNode node) {
            foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true)) {
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                    || trivia.IsKind(SyntaxKind.DisabledTextTrivia)
                    || trivia.IsDirective) {
                    return true;
                }
            }

            return false;
        }

        static bool IsAsyncVoid(MethodDeclarationSyntax method) =>
            method.Modifiers.Any(SyntaxKind.AsyncKeyword) && IsVoid(method.ReturnType);

        static bool IsVoid(TypeSyntax type) =>
            type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

        /// <summary>
        ///     ⚠ Arrangement emits one space and nothing else, and lets the formatter lay the result out
        ///     (docs/plan/06 § "Interaction with the formatter"). A rewriter that also formats is a
        ///     rewriter whose output disagrees with the formatter, and the pair stops being idempotent.
        /// </summary>
        static ArrowExpressionClauseSyntax Arrow(ExpressionSyntax expression) =>
            SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                expression.WithoutLeadingTrivia().WithTrailingTrivia()
            );

        /// <summary>
        ///     The semicolon that replaces a closing brace — carrying whatever followed that brace on
        ///     its line, because a trailing comment belongs to the author and not to the brace.
        /// </summary>
        static SyntaxToken Semicolon(SyntaxToken closeBrace) =>
            SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(closeBrace.TrailingTrivia);

        /// <summary>
        ///     The block an expression body expands back into, for <c>body = block_body</c>.
        /// </summary>
        /// <remarks>
        ///     ⚠ Which statement it becomes depends on the member: a <c>void</c> method's expression is a
        ///     statement, and everything else's is a returned value. Getting this backwards produces code
        ///     that does not compile, which is exactly what safety layer 2 exists to catch — but layer 1
        ///     is supposed to mean layer 2 never fires.
        /// </remarks>
        static BlockSyntax BlockFor(SyntaxNode member, ExpressionSyntax expression) {
            var bare = expression.WithoutLeadingTrivia().WithTrailingTrivia();
            StatementSyntax statement = member switch {
                MethodDeclarationSyntax { ReturnType: { } type } when IsVoid(type) =>
                    SyntaxFactory.ExpressionStatement(bare),
                LocalFunctionStatementSyntax { ReturnType: { } type } when IsVoid(type) =>
                    SyntaxFactory.ExpressionStatement(bare),
                ConstructorDeclarationSyntax or DestructorDeclarationSyntax =>
                    SyntaxFactory.ExpressionStatement(bare),
                AccessorDeclarationSyntax accessor when !accessor.IsKind(SyntaxKind.GetAccessorDeclaration) =>
                    SyntaxFactory.ExpressionStatement(bare),
                _ => SyntaxFactory.ReturnStatement(bare)
            };

            return SyntaxFactory.Block(statement);
        }
    }
}
