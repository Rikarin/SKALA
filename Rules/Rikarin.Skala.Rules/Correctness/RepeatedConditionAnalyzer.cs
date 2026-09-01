using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2062</c> — a later <c>else if</c> condition repeats an earlier one, so its branch is dead.
/// </summary>
/// <remarks>
///     Reaching the second condition of an <c>if</c>/<c>else if</c> chain means the first was false,
///     and <b>nothing at all runs in between</b> — no body executes, no statement intervenes. A second
///     condition equal to the first is therefore false too, and the case it was written for is never
///     handled. The code is well formed, so no compiler says a word.
///     <para>
///         ⚠
///         <b>
///             Sequential <c>if</c> statements are deliberately not compared, and that is the rule's
///             main exclusion.
///         </b> <c>if (dirty) { Flush(); } if (dirty) { … }</c> is not a defect: the
///         first body is exactly the thing that changes the answer. The "nothing ran in between"
///         argument is what makes the <c>else if</c> case decidable, and it is available nowhere else.
///     </para>
///     <para>
///         ⚠ <b>Structural equality, never text.</b> <see cref="ExpressionIdentity.Same" /> compares
///         trees and ignores layout, so a reflowed condition still matches and a condition that merely
///         <em>shares a sub-expression</em> with an earlier one does not: whole conditions are compared
///         with whole conditions, and <c>x.Kind == A</c> versus <c>x.Kind == B</c> is two questions.
///     </para>
///     <para>
///         ⚠ Both conditions must be side-effect free. <c>if (Read()) … else if (Read())</c> really is
///         two different questions, and it is the shape a rule reasoning from text alone gets wrong.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RepeatedConditionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RepeatedCondition);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (IfStatementSyntax)context.Node;

        // Only the head of a chain. Every `if` in the chain reaches the analyzer, and walking from
        // each of them would report the same pair once per rung.
        if (statement.Parent is ElseClauseSyntax) {
            return;
        }

        var conditions = new List<ExpressionSyntax>();
        for (var current = statement; current is not null;) {
            conditions.Add(current.Condition);
            current = current.Else?.Statement as IfStatementSyntax;
        }

        if (conditions.Count < 2) {
            return;
        }

        for (var i = 1; i < conditions.Count; i++) {
            var later = conditions[i];
            if (later.ContainsDiagnostics || later.ContainsDirectives || !ExpressionIdentity.IsRepeatable(later)) {
                continue;
            }

            for (var j = 0; j < i; j++) {
                var earlier = conditions[j];

                // ⚠ The first draft asked `IsRepeatable(earlier)` here as well, and a sabotage
                // proved it was dead code: `IsRepeatable` is a pure function of the syntax and
                // `Same` compares the syntax, so two equal conditions always answer it the same
                // way. The check above is the load-bearing one, and it is the only one — a second
                // copy of a guard is a second copy that no sabotage can reach.
                if (earlier.ContainsDirectives || !ExpressionIdentity.Same(earlier, later)) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        later.GetLocation(),
                        "This condition repeats the one on line "
                        + (earlier.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
                        + " of the same `if`/`else if` chain, so this branch can never run"
                    )
                );

                // One finding per repeat. A third rung equal to the first two is one mistake.
                break;
            }
        }
    }
}
