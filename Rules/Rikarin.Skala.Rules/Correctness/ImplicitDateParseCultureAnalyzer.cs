using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2162</c> — a <c>TryParse</c> of a date or a time takes its culture from whichever machine is
///     running, and the overload chosen has nowhere to say otherwise.
/// </summary>
/// <remarks>
///     ⚠ <b>This is the half of issue #244 that has no host, and the boundary was measured rather than
///     assumed.</b> <c>CA1305</c> ships in the SDK and covers the <c>Parse</c> and <c>ToString</c>
///     directions completely — a probe on a pristine <c>net10.0</c> project reports
///     <c>DateTime.Parse</c>, <c>DateTimeOffset.Parse</c>, <c>DateOnly.Parse</c>, <c>TimeOnly.Parse</c>,
///     <c>TimeSpan.Parse</c>, <c>DateTime.ToString()</c>, <c>DateOnly.ToString()</c> and
///     <c>TimeOnly.ToString()</c>. On the same probe it reports <b>no <c>TryParse</c> form at all</b>.
///     ADR-008 hosts <c>CA*</c> rather than rebuilding them, so what ships here is only the gap.
///     <para>
///         ⚠ <b>The gap matters more than its size suggests, because <c>TryParse</c> is the form the
///         documentation recommends.</b> It is what gets written wherever input might be malformed, which
///         is wherever input comes from outside the process — and that is exactly where a date arrives in
///         somebody else's culture. The exception-throwing <c>Parse</c> that <c>CA1305</c> does cover is
///         the rarer of the two in the code this analyzer exists for.
///     </para>
///     <para>
///         ⚠ <b>Only an overload with no <c>IFormatProvider</c> parameter is reported, and an explicit
///         <c>null</c> provider deliberately is not.</b> Passing <c>null</c> does mean the current
///         culture, so it looks like the same defect — but the shapes it appears in are mostly
///         <c>TryParseExact</c> with a custom format string, and a custom format string decides for
///         itself whether any culture-sensitive token is present. <c>"yyyy-MM-dd"</c> has none and is
///         correct with a <c>null</c> provider; <c>"d"</c> and <c>"MM/dd/yyyy"</c> are not. Reporting the
///         whole class would be reporting the safe majority of it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImplicitDateParseCultureAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ImplicitDateParseCulture);

    /// <summary>
    ///     ⚠ The five framework types whose textual form is culture-dependent, by metadata name.
    /// </summary>
    /// <remarks>
    ///     <c>TimeSpan</c> is here with the four date types because its own parse reads the culture's
    ///     decimal separator, and <c>CA1305</c> leaves its <c>TryParse</c> uncovered in exactly the same
    ///     way. A repository's own type of any of these names is not matched — <see cref="Clock" />
    ///     requires the symbol to come from metadata.
    /// </remarks>
    static readonly ImmutableArray<string> Types = ImmutableArray.Create(
        "System.DateTime",
        "System.DateTimeOffset",
        "System.DateOnly",
        "System.TimeOnly",
        "System.TimeSpan"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var written = invocation.Expression switch {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => null
        };

        // The cheap syntactic gate first, so the overwhelming majority of invocations never reach the
        // semantic model. `TryParseExact` is named here only to be declined below by its provider
        // parameter, which every one of its overloads has.
        if (written != "TryParse" && written != "TryParseExact") {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken)
            is not IInvocationOperation { TargetMethod.IsStatic: true } call) {
            return;
        }

        var owner = call.TargetMethod.ContainingType;
        var matched = false;
        foreach (var name in Types) {
            if (Clock.IsFrameworkType(owner, context.Compilation, name)) {
                matched = true;
                break;
            }
        }

        if (!matched || HasProvider(call.TargetMethod, context.Compilation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "`"
                + owner.Name
                + "."
                + call.TargetMethod.Name
                + "` is called on an overload that takes no format provider, so the text is read in "
                + "whatever culture the process happens to have; pass one"
            )
        );
    }

    /// <summary>
    ///     Whether the resolved overload has somewhere to put a culture.
    /// </summary>
    /// <remarks>
    ///     ⚠ The test is on the <em>parameter</em>, so it does not matter what was passed: the rule
    ///     reports the choice of an overload with no such parameter, which is the choice nobody made on
    ///     purpose. An overload that has the parameter is silence whatever its argument is — including
    ///     <c>null</c>, for the reason the type's remarks give.
    /// </remarks>
    static bool HasProvider(IMethodSymbol method, Compilation compilation) {
        var provider = compilation.GetTypeByMetadataName("System.IFormatProvider");
        if (provider is null) {
            return true;
        }

        foreach (var parameter in method.Parameters) {
            if (SymbolEqualityComparer.Default.Equals(parameter.Type, provider)) {
                return true;
            }
        }

        return false;
    }
}
