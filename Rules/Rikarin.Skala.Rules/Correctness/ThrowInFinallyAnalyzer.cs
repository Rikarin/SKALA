using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2091</c> — a <c>throw</c> that can leave a <c>finally</c> block.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2000 — Correctness". A <c>finally</c> runs while an exception
///     is in flight, and a <c>throw</c> from it <b>replaces</b> that exception rather than joining it.
///     The failure that explains everything is destroyed, and the log is left holding the cleanup's
///     complaint about a state the original failure created.
///     <para>
///         ⚠ <b>Only an explicit <c>throw</c>.</b> A <c>finally</c> that calls a method which might throw
///         is every <c>finally</c> ever written — <c>Dispose</c>, <c>Close</c> and <c>Flush</c> all can —
///         so a rule that asked "can anything in here throw" would fire on the whole tree and be switched
///         off within a day. The keyword written inside the block is the author's own statement of
///         intent, and it is the only thing this rule reads.
///     </para>
///     <para>
///         ⚠ <b>Nested ownership, so the finding lands once.</b> A <c>throw</c> inside a <c>finally</c>
///         nested in another <c>finally</c> is reported against the inner one, which is the block whose
///         author wrote it. Registering per clause and reporting every descendant would produce two
///         findings for one keyword.
///     </para>
///     <para>
///         Report-only. Deleting the <c>throw</c> discards whatever it was reporting and moving it out
///         of the <c>finally</c> changes when it runs; which of those the author meant is not visible in
///         the block.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ThrowInFinallyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ThrowInFinally);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FinallyClause);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var clause = (FinallyClauseSyntax)context.Node;

        foreach (var thrown in ExceptionFlow.Throws(clause.Block)) {
            // The nearest enclosing `finally` owns the throw. Anything nested deeper belongs to that
            // clause's own visit.
            if (thrown.FirstAncestorOrSelf<FinallyClauseSyntax>() != clause
                || !ExceptionFlow.CanEscape(thrown, clause.Block)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    thrown.GetLocation(),
                    "a `throw` from `finally` replaces the exception that was already in flight, so the "
                    + "original failure is destroyed and only the cleanup's is reported"
                )
            );
        }
    }
}
