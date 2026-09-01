using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The one question <c>SK2090</c> and <c>SK2091</c> both ask: can this <c>throw</c> leave here?
/// </summary>
/// <remarks>
///     ⚠ Shared rather than written twice, because the two rules must answer it the same way. A
///     finalizer that swallows its own exception and a <c>finally</c> that swallows its own are the same
///     shape, and a guard that drifted between the two would make one rule report what the other
///     declines to — with nothing in either message to say why.
/// </remarks>
static class ExceptionFlow {
    /// <summary>Every <c>throw</c>, in either of the two forms the language has.</summary>
    public static IEnumerable<SyntaxNode> Throws(SyntaxNode body) {
        foreach (var node in body.DescendantNodesAndSelf()) {
            if (node is ThrowStatementSyntax or ThrowExpressionSyntax) {
                yield return node;
            }
        }
    }

    /// <summary>
    ///     Whether a node reaches <paramref name="body" />'s exit rather than a <c>catch</c> or a
    ///     delegate.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>throw</c> a lambda or a local function holds is not on this body's path at all: the
    ///     delegate may never be invoked, and proving that it is would be the interprocedural analysis
    ///     both rules decline to be. Under-reporting there is the direction that keeps them usable.
    /// </remarks>
    public static bool CanEscape(SyntaxNode node, SyntaxNode body) {
        for (var current = node; current is not null && current != body; current = current.Parent) {
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) {
                return false;
            }

            // ⚠ The `try` block only. A `throw` in that statement's own `catch` or `finally` is not
            // caught by it and must keep walking outwards to find one that would be — reading this as
            // "inside a try, therefore handled" is the mistake that would silence both rules on the
            // shape they exist for.
            if (current.Parent is TryStatementSyntax { Catches.Count: > 0 } guarded && guarded.Block == current) {
                return false;
            }
        }

        return true;
    }
}
