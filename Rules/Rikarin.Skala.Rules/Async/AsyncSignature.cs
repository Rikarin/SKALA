using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     The three questions <c>SK3001</c>, <c>SK3002</c> and <c>SK3051</c> ask about a method signature
///     before they will offer to change it.
/// </summary>
/// <remarks>
///     ⚠ Shared rather than duplicated, and the duplication was real: <see cref="ImplementsAnInterface" />
///     and <see cref="RecordReference" /> stood byte for byte identical in
///     <c>AsyncVoidAnalyzer</c> and <c>UncancellableAsyncMethodAnalyzer</c>, and
///     <see cref="HasEventHandlerShape" /> in <c>AsyncVoidAnalyzer</c> and
///     <c>AsyncVoidLambdaAnalyzer</c>. All three decide whether a signature is <i>this author's</i> to
///     change, which is the precondition every one of those rules' fixes rests on — so a copy that
///     drifts does not produce a cosmetic difference, it produces a fix that breaks an override in one
///     rule and not in its sibling.
/// </remarks>
internal static class AsyncSignature {
    /// <summary>
    ///     Whether the method is somebody else's contract: an interface implementation, explicit or
    ///     implicit.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>AllInterfaces</c> rather than <c>Interfaces</c>, so a member satisfying an interface
    ///     inherited from a base type counts. Changing its return type is a break at the implementing
    ///     type even though nothing in this file mentions the interface.
    /// </remarks>
    public static bool ImplementsAnInterface(IMethodSymbol method) {
        if (!method.ExplicitInterfaceImplementations.IsEmpty) {
            return true;
        }

        var containing = method.ContainingType;
        if (containing is null) {
            return false;
        }

        foreach (var implemented in containing.AllInterfaces) {
            foreach (var member in implemented.GetMembers(method.Name)) {
                if (SymbolEqualityComparer.Default.Equals(
                        containing.FindImplementationForInterfaceMember(member),
                        method
                    )) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary><c>(object, TEventArgs)</c> — the delegate shape the BCL's events use.</summary>
    /// <remarks>
    ///     ⚠ The second parameter is walked up its base chain, so a handler taking a derived
    ///     <c>EventArgs</c> counts. <paramref name="eventArgs" /> being null means the compilation has
    ///     no <c>System.EventArgs</c>, and then nothing has the shape.
    /// </remarks>
    public static bool HasEventHandlerShape(IMethodSymbol method, INamedTypeSymbol? eventArgs) {
        if (eventArgs is null || method.Parameters.Length != 2) {
            return false;
        }

        if (method.Parameters[0].Type.SpecialType != SpecialType.System_Object) {
            return false;
        }

        for (var type = method.Parameters[1].Type; type is not null; type = type.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(type, eventArgs)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Records an identifier that names a method as a value rather than calling it — a method group.
    /// </summary>
    /// <remarks>
    ///     ⚠ The two exclusions are what make the set mean "converted to a delegate somewhere" instead
    ///     of "mentioned". A method whose name only ever appears as the callee of an invocation is never
    ///     turned into a delegate, so its signature is not pinned by a subscription and the rules may
    ///     offer to change it. Losing either exclusion silences the rule on every method that is also
    ///     called, which is nearly all of them.
    /// </remarks>
    public static void RecordReference(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<string, byte> referenced
    ) {
        var identifier = (IdentifierNameSyntax)context.Node;

        // `Foo()` — a direct call is not a method group and says nothing about a delegate.
        if (identifier.Parent is InvocationExpressionSyntax invocation
            && ReferenceEquals(invocation.Expression, identifier)) {
            return;
        }

        // `x.Foo()` — the same, one level in.
        if (identifier.Parent is MemberAccessExpressionSyntax access
            && ReferenceEquals(access.Name, identifier)
            && access.Parent is InvocationExpressionSyntax outer
            && ReferenceEquals(outer.Expression, access)) {
            return;
        }

        referenced.TryAdd(identifier.Identifier.ValueText, 0);
    }
}
