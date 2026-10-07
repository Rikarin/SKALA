using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     The empty string in the configured spelling: <c>string.Empty</c> ⇒ <c>""</c> under
///     <c>skala_empty_string = empty_literal</c>, and <c>""</c> ⇒ <c>string.Empty</c> under
///     <c>string_empty</c>.
/// </summary>
/// <remarks>
///     ⚠ SK-DIV-0013 again: the oracle performs neither direction — it normalises
///     <c>String.Empty</c> to <c>string.Empty</c> and stops — so both are fixture-pinned rather than
///     oracle-pinned, and excluded from the agreement number. Skala performs them because the export
///     asks for them when configured and doc 06 records the current direction separately.
///     <para>
///         ⚠ #383: until then only the first direction existed. <c>string_empty</c> is in the key's
///         declared domain, it is what this repository's own <c>.editorconfig</c> sets, and it did
///         nothing at all — which reads exactly like "the code already complies". The two directions are
///         not mirror images, and that is the whole of the difference between them: <c>""</c> is a
///         compile-time constant and <c>string.Empty</c> is a <c>static readonly</c> field. Every
///         position that demands a constant accepts the first and rejects the second, so the
///         <c>string.Empty</c> ⇒ <c>""</c> rewrite is total and the reverse one has to decline them all
///         (<see cref="RequiresConstant" />) — SK-DIV-0137.
///     </para>
/// </remarks>
public sealed class EmptyStringRule : ArrangementRule {
    public override string Id => ArrangeIds.EmptyString;

    public override bool NeedsSemantics => true;

    /// <remarks>
    ///     ⚠ Both values do something now, so there is no value at which the rule is off. The key is a
    ///     choice between two spellings, not a switch.
    /// </remarks>
    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        context.Options.EmptyString == EmptyStringStyle.EmptyLiteral
            ? new ToLiteral(context.Guard, context.Semantics).Visit(context.Root)
            : new ToField(context.Guard, context.Semantics).Visit(context.Root);

    /// <summary>
    ///     Whether <paramref name="node" /> sits in a position the language requires to be a constant.
    /// </summary>
    /// <remarks>
    ///     ⚠ Declined syntactically, at the rewrite, rather than left to safety layer 2. Layer 2 would catch
    ///     the resulting <c>CS0133</c>/<c>CS0182</c>/<c>CS1736</c>/<c>CS0150</c>, but its unit is the
    ///     <em>file</em>: it reverts every rewrite in it, so one <c>case "":</c> would cost the file every
    ///     other rule's arrangement and re-drop the same crash artefact on every run. The guard is the
    ///     backstop, not the mechanism.
    ///     <para>
    ///         The walk stops at the first statement, member or anonymous function that is not itself
    ///         a constant declaration: nothing further out can make this expression a constant one. A
    ///         lambda cannot appear in any of the constant positions, so a <c>""</c> in a lambda body is
    ///         never constrained by what encloses the lambda.
    ///     </para>
    /// </remarks>
    internal static bool RequiresConstant(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                // `[Obsolete("")]`, `[DefaultValue("")]`, and any expression inside them.
                case AttributeArgumentSyntax or AttributeSyntax:
                // `is ""`, `case "" when …`, a switch-expression arm's pattern, `{ Name: "" }`, `[""]`.
                case PatternSyntax:
                case CaseSwitchLabelSyntax:
                // `string s = ""` on a method, local function, lambda, indexer, delegate or primary
                // constructor parameter: a default parameter value must be a constant.
                case ParameterSyntax:
                    return true;

                case GotoStatementSyntax jump:
                    return jump.IsKind(SyntaxKind.GotoCaseStatement);

                // `const string A = "" + B;` — the literal need not be the initializer itself.
                case LocalDeclarationStatementSyntax local:
                    return local.IsConst;

                case FieldDeclarationSyntax field:
                    return field.Modifiers.Any(SyntaxKind.ConstKeyword);

                case StatementSyntax or MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax:
                    return false;
            }
        }

        return false;
    }

    sealed class ToLiteral(FormatterTagGuard guard, SemanticModel model) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (!string.Equals(node.Name.Identifier.ValueText, "Empty", StringComparison.Ordinal)) {
                return visited;
            }

            if (model.GetSymbolInfo(node).Symbol is not IFieldSymbol {
                    IsStatic: true, ContainingType.SpecialType: SpecialType.System_String
                }) {
                return visited;
            }

            // ⚠ `case string.Empty:` and `[DefaultValue(string.Empty)]` do not compile, so there is no
            // constant context to worry about in this direction: `""` is legal everywhere
            // `string.Empty` is. The rewrite is total once the symbol is confirmed.
            return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(string.Empty)
            )
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }
    }

    /// <remarks>
    ///     What counts as the empty literal, and why:
    ///     <list type="bullet">
    ///         <item>
    ///             <c>""</c> and <c>@""</c> — both are a <c>StringLiteralToken</c> whose value is empty,
    ///             and the key is about how the empty string is written, not about which quoting the
    ///             author picked for it. A verbatim empty string has no characters for the <c>@</c> to
    ///             govern.
    ///         </item>
    ///         <item>
    ///             <c>""u8</c> is declined: it is a <c>ReadOnlySpan&lt;byte&gt;</c>, not a string, and
    ///             <c>string.Empty</c> would not even convert to it. Its expression kind is
    ///             <c>Utf8StringLiteralExpression</c>, so the kind test excludes it.
    ///         </item>
    ///         <item>
    ///             <c>$""</c> and <c>$@""</c> are declined: they are interpolated-string expressions,
    ///             not literals, and replacing one is a different rewrite (removing a redundant
    ///             <c>$</c>) that this key does not ask for.
    ///         </item>
    ///         <item>
    ///             A raw string literal cannot be empty — <c>""""""</c> is a parse error, and every
    ///             legal raw literal has content — so its token kinds never match.
    ///         </item>
    ///         <item>
    ///             ⚠ Inside an expression tree it is declined although it would compile. <c>""</c> is a
    ///             <c>ConstantExpression</c> in the tree and <c>string.Empty</c> a <c>MemberExpression</c>
    ///             over a field, and the tree is data a query provider translates: the rewrite would
    ///             change what the provider is handed, not merely how the source is spelled.
    ///         </item>
    ///     </list>
    /// </remarks>
    sealed class ToField(FormatterTagGuard guard, SemanticModel model) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node) {
            if (!node.IsKind(SyntaxKind.StringLiteralExpression)
                || !node.Token.IsKind(SyntaxKind.StringLiteralToken)
                || node.Token.ValueText.Length != 0
                || RequiresConstant(node)
                || ExpressionTreeContext.Contains(model, node)) {
                return node;
            }

            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName("Empty")
            )
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }
    }
}
