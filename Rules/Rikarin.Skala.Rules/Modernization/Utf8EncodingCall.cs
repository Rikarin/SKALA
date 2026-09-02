using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     Binding a call to the framework's <c>System.Text.Encoding.UTF8</c>, which <c>SK1060</c> and
///     <c>SK1061</c> both do before they will rewrite one.
/// </summary>
/// <remarks>
///     ⚠ Shared rather than duplicated, and the clause worth naming is
///     <c>Locations.Any(location =&gt; location.IsInSource)</c>: it rejects a <c>System.Text.Encoding</c>
///     <i>declared in the compilation being analysed</i>. Both rules replace a call with a language
///     construct that binds to the real BCL type, so a project that declares its own type of that name
///     — a shim, a test double, a trimmed reimplementation — must be left alone or the fix rewrites a
///     call to somebody else's method into a call to the BCL's. That guard is one line, it is the only
///     thing standing between the rules and that outcome, and it stood copied.
/// </remarks>
internal static class Utf8EncodingCall {
    /// <summary>
    ///     The bound call, when <paramref name="invocation" /> is a one-parameter method on the
    ///     framework's <c>Encoding</c> reached through its static <c>UTF8</c> property; otherwise null.
    /// </summary>
    /// <remarks>
    ///     ⚠ The parameter's <i>type</i> is deliberately not checked here — <c>SK1060</c> wants
    ///     <c>byte[]</c> and <c>SK1061</c> wants <c>string</c>. Only the arity is shared, and that is
    ///     what stops the overloads taking a span or a range from binding.
    /// </remarks>
    public static IInvocationOperation? Bind(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellation
    ) {
        var encoding = model.Compilation.GetTypeByMetadataName("System.Text.Encoding");
        if (encoding is null
            || encoding.Locations.Any(static location => location.IsInSource)
            || model.GetOperation(invocation, cancellation) is not IInvocationOperation call
            || !SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, encoding)
            || call.TargetMethod.Parameters.Length != 1
            || call.Instance is not IPropertyReferenceOperation {
                Property.Name: "UTF8",
                Property.IsStatic: true
            } receiver
            || !SymbolEqualityComparer.Default.Equals(receiver.Property.ContainingType, encoding)) {
            return null;
        }

        return call;
    }
}
