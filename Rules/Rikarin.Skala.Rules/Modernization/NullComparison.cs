using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     The one question <c>SK1010</c> has to answer: is <c>x == null</c> the same program as
///     <c>x is null</c> here? (<c>SK1020</c> asked it too, until #281 retired it.)
/// </summary>
/// <remarks>
///     ⚠ It is not, in general. <c>==</c> calls whatever <c>operator ==</c> the operand's type
///     declares; <c>is null</c> is a pattern and always tests reference (or default) equality. On a
///     type with a user-defined operator the two can differ arbitrarily, and rewriting one into the
///     other is then a behaviour change dressed as a style fix. So the answer is "yes" only where the
///     type is resolved <em>and</em> declares no such operator.
/// </remarks>
internal static class NullComparison {
    /// <summary>
    ///     ⚠ The one documented exception, and it is a verified one rather than a convenience.
    ///     <c>System.String</c> declares <c>operator ==</c>, and that operator is
    ///     <c>((object)a == (object)b) || (a is not null &amp;&amp; b is not null &amp;&amp; …)</c> —
    ///     with one side the null literal it reduces to exactly the reference test the pattern makes.
    ///     Excluding it would make the rule silent on the single most common null check in C#, which
    ///     is most of its value; including anything else on this list would need the same proof.
    /// </summary>
    static bool IsProvenEquivalent(ITypeSymbol type) => type.SpecialType == SpecialType.System_String;

    /// <summary>The non-null side of a null comparison, or null when this is not one.</summary>
    public static ExpressionSyntax? OperandOf(BinaryExpressionSyntax binary) {
        if (!binary.IsKind(SyntaxKind.EqualsExpression) && !binary.IsKind(SyntaxKind.NotEqualsExpression)) {
            return null;
        }

        var leftIsNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
        var rightIsNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
        if (leftIsNull == rightIsNull) {
            return null;
        }

        return leftIsNull ? binary.Right : binary.Left;
    }

    /// <summary>Whether rewriting a null comparison on <paramref name="operand" /> preserves meaning.</summary>
    public static bool IsRewritable(SemanticModel model, ExpressionSyntax operand, CancellationToken cancellation) {
        var type = model.GetTypeInfo(operand, cancellation).Type;
        if (type is null
            || type.TypeKind == TypeKind.Error
            || type.TypeKind == TypeKind.Dynamic
            || type.TypeKind == TypeKind.Pointer
            || type.TypeKind == TypeKind.FunctionPointer) {
            return false;
        }

        // ⚠ An unconstrained type parameter's `== null` is its own thing — it is only legal at all
        // because the compiler lifts it, and what it means depends on the type argument.
        if (type.TypeKind == TypeKind.TypeParameter) {
            return false;
        }

        // `int?` and friends: Nullable<T> declares no operator of its own and `is null` is exactly
        // `HasValue == false`.
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) {
            return true;
        }

        if (!type.IsReferenceType) {
            return false;
        }

        if (IsProvenEquivalent(type)) {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var member in current.GetMembers()) {
                if (member is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } method
                    && (method.Name == "op_Equality" || method.Name == "op_Inequality")) {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     ⚠ Whether the expression sits inside an expression tree, where a pattern does not compile.
    /// </summary>
    /// <remarks>
    ///     <c>Expression&lt;Func&lt;T, bool&gt;&gt; f = x =&gt; x.Name != null;</c> is legal and
    ///     <c>x =&gt; x.Name is not null</c> is CS8122. A fix that does not compile is worse than no
    ///     fix, so the whole finding is withheld rather than the fix alone — a finding an agent cannot
    ///     act on is a finding that teaches it to ignore the tool.
    /// </remarks>
    public static bool InsideExpressionTree(
        SemanticModel model,
        SyntaxNode node,
        CancellationToken cancellation
    ) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            if (current is not (SimpleLambdaExpressionSyntax
                    or ParenthesizedLambdaExpressionSyntax
                    or AnonymousMethodExpressionSyntax)) {
                continue;
            }

            var converted = model.GetTypeInfo(current, cancellation).ConvertedType;
            for (var type = converted; type is not null; type = type.BaseType) {
                if (type.ToDisplayString()
                        .StartsWith("System.Linq.Expressions.Expression", System.StringComparison.Ordinal)) {
                    return true;
                }
            }
        }

        return false;
    }
}
