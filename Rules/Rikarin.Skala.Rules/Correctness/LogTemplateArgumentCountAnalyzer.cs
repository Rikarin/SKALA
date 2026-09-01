using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2070</c> — a Serilog message template with a different number of holes than the call
///     supplies values for.
/// </summary>
/// <remarks>
///     The rendered message is wrong in both directions: a hole with no value renders as the literal
///     <c>{Name}</c>, and a value with no hole is attached to the event under a fabricated positional
///     key nobody queries.
///     <para>
///         ⚠ <b>Serilog only, and that is the whole scope decision.</b> <c>CA2017</c> — "number of
///         parameters supplied in the logging message template do not match the number of named
///         placeholders" — already covers <c>Microsoft.Extensions.Logging</c>, and it covers it
///         thoroughly: measured against <c>LoggerExtensions</c>, <c>ILogger.BeginScope</c> and
///         <c>LoggerMessage.Define</c>, with <c>{{</c> escapes and <c>{X,10:N2}</c> alignment handled
///         and a non-implicit <c>params</c> array correctly declined. ⚠ <b>It is also on by default</b>,
///         unlike <c>CA2241</c>, <c>CA2253</c> and <c>CA2254</c>, which need an
///         <c>AnalysisMode</c> the default project does not set. ADR-008 hosts <c>CA*</c> rather than
///         rebuilding them, so the only part of this concept Skala implements is the part with no host
///         at all: Serilog, which <c>CA2017</c> does not know exists.
///     </para>
///     <para>
///         ⚠ <b>Arity is counted in holes, not in distinct names.</b> <c>"{X} then {X}"</c> with two
///         arguments is correct — the values are bound to holes in order — and reporting it here would
///         be a false positive on the shape <c>SK2071</c> exists to describe properly.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LogTemplateArgumentCountAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.LogTemplateArgumentCount);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var serilog = MessageTemplate.ResolveSerilog(start.Compilation);
                if (serilog.IsEmpty) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, serilog),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> serilog) {
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken)
            is not IInvocationOperation operation) {
            return;
        }

        if (!MessageTemplate.DeclaredBy(operation, serilog)
            || MessageTemplate.FindTemplate(operation) is not { } template
            || template.Value.ConstantValue is not { HasValue: true, Value: string text }
            || !MessageTemplate.TryReadValues(operation, template, out var supplied)) {
            return;
        }

        var parsed = MessageTemplate.Parse(text);

        // ⚠ A positional template is Serilog's `string.Format` mode; arity there is max-index-plus-one
        // and mixing it with names has semantics no rule should guess at. CA2253 says not to write one.
        if (parsed.Positional || parsed.Holes.Count == supplied) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                template.Value.Syntax.GetLocation(),
                "the template has "
                + Count(parsed.Holes.Count, "hole")
                + " and the call supplies "
                + Count(supplied, "value")
            )
        );
    }

    static string Count(int n, string noun) =>
        n.ToString(CultureInfo.InvariantCulture) + " " + noun + (n == 1 ? string.Empty : "s");
}
