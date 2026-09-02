using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2212</c> — a loop whose body always jumps out, so it is an <c>if</c> in a loop's clothing.
/// </summary>
/// <remarks>
///     Either the jump is in the wrong place — it belongs under a condition that is missing — or the
///     loop is, and what was wanted is <c>FirstOrDefault</c>, a single <c>if</c>, or an index. The
///     shape survives review because the word <c>foreach</c> promises repetition the body has already
///     ruled out.
///     <para>
///         ⚠ <b>The question is settled by control flow, not by looking at the last statement.</b> That
///         is what makes it decidable rather than a heuristic, and it is also what gets <c>break</c>
///         right inside a nested <c>switch</c>, where the <c>break</c> binds to the switch and leaves
///         the loop running. <c>AnalyzeControlFlow</c> over the body region binds every jump to its own
///         enclosing statement, so a <c>switch</c>'s <c>break</c> and a nested lambda's <c>return</c>
///         are excluded without the rule having to know about either.
///     </para>
///     <para>
///         ⚠ <b>An unreachable endpoint is not by itself a body that jumps out.</b> Control can also
///         fail to reach the end because a statement never completes, and in practice that is a nested
///         constant-condition loop. A body containing one is declined, as is a body containing any
///         <c>goto</c>, whose cycles this analysis does not chase.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleIterationLoopAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SingleIterationLoop);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ForEachStatement,
            SyntaxKind.ForEachVariableStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var loop = context.Node;
        if (BodyOf(loop) is not { } body || !HasStatements(body)) {
            return;
        }

        // ⚠ The two confounds that also make an endpoint unreachable, ruled out before the flow
        // question is asked rather than after it.
        if (ContainsUnterminatingConstruct(body)) {
            return;
        }

        var flow = context.SemanticModel.AnalyzeControlFlow(body);
        if (flow is not { Succeeded: true, EndPointIsReachable: false }) {
            return;
        }

        // ⚠ `continue` ends the iteration, not the loop. A body ending in one has an unreachable
        // endpoint and still runs to completion, so it is the trap this check exists for — and one
        // `continue` among several exits is enough, because that path alone iterates again.
        //
        // ⚠ No exit point at all is not a miss: with `goto` and non-terminating nested loops already
        // excluded above, the only remaining way to make the endpoint unreachable is a `throw` on
        // every path, which is a body that runs at most once for the same reason.
        if (flow.ExitPoints.Any(static point => point.IsKind(SyntaxKind.ContinueStatement))) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                loop.ChildTokens().First().GetLocation(),
                "every path through this body jumps out of the loop, so it runs at most once"
            )
        );
    }

    static StatementSyntax? BodyOf(SyntaxNode loop) =>
        loop switch {
            CommonForEachStatementSyntax forEach => forEach.Statement,
            ForStatementSyntax forStatement => forStatement.Statement,
            WhileStatementSyntax whileStatement => whileStatement.Statement,
            DoStatementSyntax doStatement => doStatement.Statement,
            _ => null
        };

    /// <summary>An empty body has no jump in it, and an empty loop is a different finding.</summary>
    /// <remarks>
    ///     ⚠ <b>Statement of intent, not the mechanism.</b> A sabotage removed it and no fixture went
    ///     red: control falls straight off the end of an empty block, so <c>EndPointIsReachable</c> is
    ///     already <c>true</c> and the flow test declines it a step later. Kept because it says what
    ///     the rule means and saves the analysis, not because anything depends on it.
    /// </remarks>
    static bool HasStatements(StatementSyntax body) =>
        body is not EmptyStatementSyntax && (body is not BlockSyntax block || block.Statements.Count > 0);

    /// <summary>
    ///     ⚠ A nested loop that never completes, or any <c>goto</c>, makes the endpoint unreachable for
    ///     a reason that is not "every path jumps out".
    /// </summary>
    static bool ContainsUnterminatingConstruct(StatementSyntax body) {
        foreach (var node in body.DescendantNodesAndSelf()) {
            if (node.IsKind(SyntaxKind.GotoStatement)
                || node.IsKind(SyntaxKind.GotoCaseStatement)
                || node.IsKind(SyntaxKind.GotoDefaultStatement)) {
                return true;
            }

            var condition = node switch {
                ForStatementSyntax nested => nested.Condition,
                WhileStatementSyntax nested => nested.Condition,
                DoStatementSyntax nested => nested.Condition,
                _ => null
            };

            // ⚠ `for (;;)` has no condition at all and is the same infinite loop as `while (true)`.
            if (node is ForStatementSyntax { Condition: null }
                || (condition is not null && condition.IsKind(SyntaxKind.TrueLiteralExpression))) {
                return true;
            }
        }

        return false;
    }
}
