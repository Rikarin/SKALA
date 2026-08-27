using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// <c>x != null</c> ⇒ <c>x is not null</c>, and <c>x == null</c> ⇒ <c>x is null</c>.
/// </summary>
/// <remarks>
/// ⚠ <b>SK-DIV-0013.</b> Two things are true about this rule and neither is obvious.
/// <para>
/// First, the oracle does not perform it. <c>resharper_null_checking_pattern_style =
/// not_null_pattern</c> is set in the export and <c>jb cleanupcode</c> 2025.2.6 rewrites nothing —
/// under any profile shape, with the inspection at its exported <c>hint</c> or raised to
/// <c>warning</c> (the sweep is in <c>docs/oracle-cleanup-profile.md</c>). The reading that fits is
/// that the key governs the pattern ReSharper *generates* in a quick-fix, not a cleanup of code that
/// already exists. So this rule is pinned by hand-written fixtures and is excluded from the
/// changed-span agreement number: measuring it against an oracle that never moves would score every
/// correct rewrite as a divergence.
/// </para>
/// <para>
/// Second, and this is the part that matters for safety: <c>!= null</c> and <c>is not null</c> are
/// <b>not the same expression</b> when the operand's type declares <c>operator ==</c>. The operator
/// form calls the user's operator; the pattern form is a reference comparison the language performs
/// itself. Rewriting <c>a != null</c> on such a type silently changes which code runs, and it still
/// compiles — the exact class of bug layer 3 exists for, arriving through a rewrite that layer 3
/// cannot see because no *identifier* changed meaning. docs/plan/06 § "Null and pattern style":
/// Skala checks for a user-defined <c>operator ==</c> on the operand type and skips the rewrite when
/// one exists, taking the safe side and reporting the divergence in <c>skala config explain</c>.
/// </para>
/// </remarks>
public sealed class NullCheckingPatternRule : ArrangementRule {
    public override string Id => ArrangeIds.NullCheckingPattern;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.NullCheckingPattern == NullCheckingPatternStyle.NotNullPattern;

    public override SyntaxNode Apply(ArrangementContext context) => new Rewriter(context.Semantics).Visit(context.Root);

    /// <summary>
    /// Whether <paramref name="type"/> declares a user-defined <c>operator ==</c>, anywhere in its
    /// inheritance chain.
    /// </summary>
    /// <remarks>
    /// ⚠ Public so that <c>skala config explain</c> can name the condition rather than describe it,
    /// and so that the fixture set can assert on it directly.
    /// <para>
    /// ⚠ The base chain is walked because an operator declared on a base class applies to a derived
    /// operand: checking only the operand's own members lets exactly the dangerous case through.
    /// <c>object</c> itself is excluded — its <c>==</c> is the reference comparison the pattern form
    /// performs, so the two agree there and stopping at it would refuse every rewrite.
    /// </para>
    /// </remarks>
    public static bool DeclaresEqualityOperator(ITypeSymbol? type) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (current.SpecialType is SpecialType.System_Object or SpecialType.System_String) {
                // `string`'s `==` is value equality and the pattern form matches it; `object`'s is
                // the reference comparison the pattern performs.
                return false;
            }

            foreach (var member in current.GetMembers(WellKnownMemberNames.EqualityOperatorName)) {
                if (member is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator }) {
                    return true;
                }
            }
        }

        return false;
    }

    sealed class Rewriter(SemanticModel model) : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node) {
            var visited = (BinaryExpressionSyntax)base.VisitBinaryExpression(node)!;
            if (!node.IsKind(SyntaxKind.EqualsExpression) && !node.IsKind(SyntaxKind.NotEqualsExpression)) {
                return visited;
            }

            var negated = node.IsKind(SyntaxKind.NotEqualsExpression);
            var (operand, visitedOperand) = IsNullLiteral(node.Right)
                ? (node.Left, visited.Left)
                : IsNullLiteral(node.Left)
                ? (node.Right, visited.Right)
                : (null, null);

            if (operand is null || visitedOperand is null || !IsSafe(operand)) {
                return visited;
            }

            PatternSyntax pattern = SyntaxFactory.ConstantPattern(
                SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
            );

            if (negated) {
                pattern = SyntaxFactory.UnaryPattern(
                    SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                    pattern
                );
            }

            return SyntaxFactory.IsPatternExpression(
                visitedOperand.WithoutTrailingTrivia(),
                SyntaxFactory.Token(SyntaxKind.IsKeyword)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                pattern
            )
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        bool IsSafe(ExpressionSyntax operand) {
            var type = model.GetTypeInfo(operand).Type;
            if (type is null || type.TypeKind == TypeKind.Error) {
                return false;
            }

            // ⚠ A value type that is not `Nullable<T>` cannot be compared to null at all, and an
            // unconstrained type parameter's `is null` binds differently from its `== null`.
            if (type.TypeKind == TypeKind.TypeParameter || type.TypeKind == TypeKind.Pointer) {
                return false;
            }

            if (type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T) {
                return false;
            }

            // The divergence. See the type remarks.
            return !DeclaresEqualityOperator(type);
        }

        static bool IsNullLiteral(ExpressionSyntax expression) =>
            expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
    }
}
