using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7091</c> — <c>Environment.Exit</c> called anywhere but the entry point.
/// </summary>
/// <remarks>
///     <c>Environment.Exit</c> does not unwind. No <c>finally</c> runs, no <c>using</c> disposes, no
///     buffered writer flushes, and no <c>await</c> resumes. In a library, in a service, and in an
///     executable's own helper class alike, it ends work that has nothing to do with whoever called it.
///     <para>
///         ⚠
///         <b>
///             The rule does not try to tell an application from a library, and that is a measurement
///             rather than a shrug.
///         </b> <c>LooseLoader</c> constructs its compilation with
///         <c>OutputKind.DynamicallyLinkedLibrary</c>, so "this compilation is a library" and "no
///         project file was loaded" are the same observation — and loose is the mode Skala exists for,
///         because a folder of generated <c>.cs</c> files has no project. A rule keyed on
///         <c>OutputKind</c> would therefore report every console application analysed without its
///         project file, which is the false-positive engine that gets the analysis half switched off.
///     </para>
///     <para>
///         So the line drawn is a different one, and it holds under every load mode: the entry point
///         may end the process, because ending it is the process's own decision and there is nothing
///         above to unwind into. Everywhere else the call destroys cleanup somebody else wrote. The
///         entry point is the compilation's own when there is one, and otherwise a static
///         <c>Main</c> by name — that fallback exists precisely because loose mode has no entry point
///         to ask for.
///     </para>
///     <para>
///         ⚠ <c>Environment.FailFast</c> is deliberately not reported. Skipping cleanup is the whole
///         point of it: it is what an author writes when the state is corrupt enough that running
///         <c>finally</c> blocks would be the more dangerous act, and it writes a dump saying so.
///         Reporting it would be reporting a decision rather than an accident.
///     </para>
///     <para>
///         Report-only. The repair is to return, to throw, or to set an exit code and let the entry
///         point end the process — which of those is right depends on what the caller is owed, and an
///         edit that guessed would change control flow on a guess.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessExitAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ProcessExitOutsideEntryPoint);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var environment = start.Compilation.GetTypeByMetadataName("System.Environment");
                if (environment is null) {
                    return;
                }

                var entryPoint = start.Compilation.GetEntryPoint(start.CancellationToken);
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, environment, entryPoint),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol environment,
        IMethodSymbol? entryPoint
    ) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ The type is resolved, never matched on the written name. `Environment` is a plausible
        // name for somebody's own type and an `Exit` on one of those is not this.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { Name: "Exit" } target
            || !SymbolEqualityComparer.Default.Equals(target.ContainingType, environment)) {
            return;
        }

        if (IsInsideEntryPoint(context, entryPoint)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "the process ends here without running `finally`, `IDisposable` cleanup or buffered "
                + "writes that code above this call is relying on"
            )
        );
    }

    static bool IsInsideEntryPoint(SyntaxNodeAnalysisContext context, IMethodSymbol? entryPoint) {
        // Top-level statements: the containing symbol is synthesized, so the syntax is what says so.
        if (context.Node.Ancestors().Any(static node => node is GlobalStatementSyntax)) {
            return true;
        }

        for (var symbol = context.ContainingSymbol; symbol is IMethodSymbol method; symbol = method.ContainingSymbol) {
            if (SymbolEqualityComparer.Default.Equals(method, entryPoint)) {
                return true;
            }

            // ⚠ The name fallback, and it is load-mode insurance rather than laziness. A loose
            // compilation is built as a library, so it has no entry point to compare against and
            // every console application's `Main` would be reported without this.
            if (method is { IsStatic: true, Name: "Main" }) {
                return true;
            }
        }

        return false;
    }
}
