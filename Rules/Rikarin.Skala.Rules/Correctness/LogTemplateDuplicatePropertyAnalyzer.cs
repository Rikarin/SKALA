using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2071</c> — a structured log template naming the same property twice.
/// </summary>
/// <remarks>
///     The rendered line looks right and the structured event is wrong: one key cannot hold two
///     values, so a sink keeps one of them and the other is gone. Which one survives is the sink's
///     business, so the field a dashboard filters on is silently either the first value or the last.
///     <para>
///         ⚠ <b>Nothing hosts this, for any logging framework.</b> <c>CA2017</c> counts <em>holes</em>
///         against arguments, so <c>logger.LogInformation("{X} then {X}", a, b)</c> is arity-correct and
///         it stays silent — measured in a probe project at every analysis mode, not read from
///         documentation. That is why this rule covers <c>Microsoft.Extensions.Logging</c> as well as
///         Serilog, where <see cref="LogTemplateArgumentCountAnalyzer" /> deliberately does not: the
///         concept has no host anywhere, so ADR-008 has nothing to defer to.
///     </para>
///     <para>
///         ⚠ <b><c>{@Order}</c> and <c>{Order}</c> are the same property</b> and are reported. The
///         destructuring sigil selects how the value is captured, not what it is called, so a parser
///         that keeps the sigil sees two names where the logger sees one and misses the duplicate this
///         rule exists to find.
///     </para>
///     <para>
///         ⚠ <b>The finding is reported on the whole template argument, not on the repeated name.</b>
///         A hole's offset is known within the template's <em>value</em>, and the value and the source
///         spelling diverge at the first escape sequence — one <c>\n</c> earlier in the literal and a
///         span computed from the value points one character short. A slightly coarse span that is
///         always right beats a precise one that is quietly wrong in a baseline fingerprint.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LogTemplateDuplicatePropertyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.LogTemplateDuplicateProperty);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var loggers = MessageTemplate.ResolveLoggers(start.Compilation);
                if (loggers.IsEmpty) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, loggers),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> loggers) {
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken)
            is not IInvocationOperation operation
            || !MessageTemplate.DeclaredBy(operation, loggers)
            || MessageTemplate.FindTemplate(operation) is not { } template
            || template.Value.ConstantValue is not { HasValue: true, Value: string text }) {
            return;
        }

        var parsed = MessageTemplate.Parse(text);
        if (parsed.Positional) {
            return;
        }

        // ⚠ Ordinal, so `{Count}` and `{count}` are two properties and neither is reported. They are
        // two properties to every sink there is; calling them a duplicate would be an opinion about
        // naming dressed as a defect, and the value that would be lost is not lost.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hole in parsed.Holes) {
            if (seen.Add(hole.Name) || !reported.Add(hole.Name)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    template.Value.Syntax.GetLocation(),
                    "the template names `"
                    + hole.Name
                    + "` more than once; one key cannot hold two values, so a sink keeps one and "
                    + "discards the other"
                )
            );
        }
    }
}
