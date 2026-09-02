using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary>
///     ⚠ The one question to ask before handing a rewritten node to
///     <c>GetSpeculativeSymbolInfo</c>.
/// </summary>
/// <remarks>
///     <para>
///         Speculative binding is how the cleanup rules prove a deletion keeps the program the same:
///         rewrite the node, bind the rewrite at the original position, and demand the same symbol
///         back. The node handed over is <em>detached</em> — it has no parent — and for almost every
///         expression that is fine, because binding needs only the position and the node.
///     </para>
///     <para>
///         ⚠ <b>A member binding is the exception, and it does not fail politely.</b> The
///         <c>.Where(…)</c> in <c>values?.Where(…).Cast&lt;string&gt;()</c> is a
///         <c>MemberBindingExpressionSyntax</c>, and its receiver is the conditional access
///         <em>above</em> it. Detach the subtree and that access is gone, so Roslyn's own
///         <c>FindConditionalAccessNodeForBinding</c> walks off the top of the tree and dereferences
///         null — a <c>NullReferenceException</c> thrown by the compiler, inside the analyzer, which
///         surfaces as <c>AD0001</c> and therefore as nothing at all in a report (#279, #295). It was
///         live in <c>SK0234</c> and was found by asserting on <c>AD0001</c> rather than by reading
///         the code.
///     </para>
/// </remarks>
static class SpeculativeBinding {
    /// <summary>
    ///     Whether <paramref name="node" /> can be bound on its own, away from the tree it came from.
    /// </summary>
    /// <remarks>
    ///     A member binding is safe exactly when the conditional access it belongs to travels with it.
    ///     <c>Foo&lt;int&gt;(x?.Y)</c> keeps its <c>x?.Y</c> inside the node and binds; the
    ///     <c>WhenNotNull</c> half of a conditional access does not, and is refused.
    /// </remarks>
    public static bool CanBindDetached(ExpressionSyntax node) {
        foreach (var descendant in node.DescendantNodesAndSelf()) {
            if (descendant is not MemberBindingExpressionSyntax binding) {
                continue;
            }

            var access = binding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>();
            if (access is null || !node.Span.Contains(access.Span)) {
                return false;
            }
        }

        return true;
    }
}
