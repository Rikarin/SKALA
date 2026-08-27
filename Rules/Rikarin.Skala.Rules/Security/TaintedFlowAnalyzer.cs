using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
/// The half of <c>SK5001</c> and <c>SK5002</c> that is identical: find the sinks cheaply, and only
/// then pay for a control-flow graph.
/// </summary>
/// <remarks>
/// ⚠ <b>The laziness is the point of this class.</b> docs/plan/13 § "Warm analysis, changed files"
/// budgets five seconds for a changed-file run, and taint analysis is the most expensive thing in
/// the tool. Two gates stand in front of it, cheapest first:
/// <list type="number">
/// <item>
/// <b>Per compilation.</b> <see cref="TaintSymbols.For"/> resolves the declared source and sink
/// types once. A tree that references neither an HTTP server nor any of a rule's sink types
/// registers <em>no actions at all</em> — not a cheap action, none — so the rule's cost on such a
/// tree is a handful of <c>GetTypeByMetadataName</c> calls for the whole run.
/// </item>
/// <item>
/// <b>Per operation block.</b> A tree walk looks for an operation that resolves to one of the
/// rule's sinks. Only a method that has one is worth a control-flow graph, and almost none does.
/// </item>
/// </list>
/// ⚠ It is a static helper rather than a base class on purpose. Roslyn's own
/// <c>RS2002</c> reads an analyzer type looking for the descriptors it supports and does not see
/// through an inherited <c>SupportedDiagnostics</c>, so an abstract base would put both rules
/// outside release tracking — a guard silently switched off, which is the failure mode
/// <c>ToolDiagnosticIdTests</c> exists to remember.
/// </remarks>
public static class TaintedFlow {
    /// <summary>Wires one rule's sinks into a compilation, or declines to register anything.</summary>
    public static void Register(AnalysisContext context, string ruleId, DiagnosticDescriptor descriptor) =>
        context.RegisterCompilationStartAction(start => {
                // Gate 1. See the type's remarks: no trust boundary, or no sink of this rule's kind
                // in this compilation, and nothing at all is registered.
                if (TaintSymbols.For(start.Compilation, ruleId) is not { } symbols) {
                    return;
                }

                start.RegisterOperationBlockAction(block => Analyze(block, symbols, descriptor));
            }
        );

    static void Analyze(
        OperationBlockAnalysisContext context,
        TaintSymbols symbols,
        DiagnosticDescriptor descriptor
    ) {
        foreach (var block in context.OperationBlocks) {
            // ⚠ Only the bodies a control-flow graph can be built from. A field initialiser, a
            // property expression body and an attribute argument are operation blocks too, and
            // asking for the graph of one that is not a body throws rather than returning null.
            if (block.Kind is not (OperationKind.Block or OperationKind.MethodBody
                    or OperationKind.ConstructorBody)) {
                continue;
            }

            // Gate 2. A tree walk is cheap; a control-flow graph and a dataflow fixpoint are not.
            if (!ContainsSink(block, symbols, context.CancellationToken)) {
                continue;
            }

            var graph = context.GetControlFlowGraph(block);
            foreach (var finding in TaintAnalysis.Run(graph, symbols, context.CancellationToken)) {
                context.ReportDiagnostic(Diagnostic.Create(descriptor, finding.Location, Message(finding)));
            }
        }
    }

    /// <summary>
    /// Whether this body mentions one of the rule's sinks at all.
    /// </summary>
    /// <remarks>
    /// ⚠ Symbol-level, not name-level. A method called <c>Start</c> on a user's own type is not
    /// <c>Process.Start</c>, and a name test would put every one of them through the engine.
    /// </remarks>
    static bool ContainsSink(IOperation operation, TaintSymbols symbols, CancellationToken cancellation) {
        cancellation.ThrowIfCancellationRequested();

        var found = operation switch {
            ISimpleAssignmentOperation { Target: IPropertyReferenceOperation reference } =>
                symbols.Sink(reference.Property) is { Kind: "property" },
            IInvocationOperation invocation => symbols.Sink(invocation.TargetMethod) is { Kind: "method" },
            IObjectCreationOperation { Constructor: { } constructor } =>
                symbols.Sink(constructor) is { Kind: "constructor" },
            _ => false
        };

        if (found) {
            return true;
        }

        foreach (var child in operation.ChildOperations) {
            if (ContainsSink(child, symbols, cancellation)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ⚠ The message names the value, what it reaches, and what to write instead — all three.
    /// </summary>
    /// <remarks>
    /// docs/plan/00: a diagnostic "carries the rule ID, one sentence of <em>why</em>, and either a
    /// fix or a concrete 'do this instead'". docs/plan/10 § "The MCP server" is why it matters more
    /// here than anywhere else: an agent that cannot see what to do instead suppresses the rule, and
    /// a suppressed <c>SK5xxx</c> is worse than a rule that never shipped. The "instead" text lives
    /// in <c>taint.json</c> beside the sink it belongs to, so a new sink arrives with its own advice
    /// rather than inheriting a generic sentence.
    /// </remarks>
    static string Message(TaintFinding finding) =>
        "`"
        + finding.SourceDescription
        + "` came from the request and reaches "
        + finding.Sink.What
        + "; "
        + finding.Sink.Instead;
}

/// <summary>
/// <c>SK5001</c> — a value from the request, concatenated into SQL that a command executes.
/// </summary>
/// <remarks>
/// docs/plan/08 § "SK5000 — Security". The oldest bug in the catalogue and still the most expensive
/// one: the injected text is parsed as SQL, so it is not "some data was wrong", it is an arbitrary
/// second statement running with the application's own database credentials.
/// <para>
/// ⚠ The rule reports only where the flow is <em>proven</em> inside one method. See
/// <see cref="TaintAnalysis"/> for what that excludes and why — in particular that a parameter is
/// never a source, which is a statement about what the engine can honestly know rather than a
/// concession to any particular tree.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SqlInjectionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SqlFromRequestConcatenation);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        TaintedFlow.Register(context, RuleIds.SqlFromRequestConcatenation, Descriptor);
    }
}

/// <summary>
/// <c>SK5002</c> — a value from the request, reaching which program is started or its command line.
/// </summary>
/// <remarks>
/// docs/plan/08 § "SK5000 — Security".
/// <para>
/// ⚠ <c>ProcessStartInfo.ArgumentList</c> is deliberately <b>not</b> a sink, and that is the
/// rule's recommendation rather than an omission. <c>Arguments</c> is one string that the child's
/// own startup code re-splits, so an embedded quote becomes a second argument; <c>ArgumentList</c>
/// hands each element to the child verbatim on Unix and does the quoting itself on Windows, so
/// there is no syntax left for an attacker to escape. A rule that reported both would be telling a
/// reader that the fix it recommends is also a finding.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessArgumentInjectionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ProcessStartFromRequest);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        TaintedFlow.Register(context, RuleIds.ProcessStartFromRequest, Descriptor);
    }
}
