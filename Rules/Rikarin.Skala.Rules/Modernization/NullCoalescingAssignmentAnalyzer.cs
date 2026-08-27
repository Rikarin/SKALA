using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1030</c> — <c>x = x ?? y</c> is <c>x ??= y</c>.
/// </summary>
/// <remarks>
///     ⚠ The whole rule is in the guard. <c>a[i] = a[i] ?? b</c> evaluates the indexer twice and
///     <c>a[i] ??= b</c> evaluates it once; for a side-effecting indexer those are different programs,
///     and a "safe" fix that changes how many times a thing runs is not a safe fix. The target is
///     therefore required to be a chain of plain names — the only shape where re-evaluation is
///     provably free.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullCoalescingAssignmentAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.NullCoalescingAssignment);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullCoalescingAssignment);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleAssignmentExpression);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (assignment.Right is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce) {
            return;
        }

        // ⚠ An expression statement, so that the assignment's own value is discarded. `f(x = x ?? y)`
        // has the same value either way, but restricting to statements keeps the rewrite obviously
        // local and costs nothing real.
        if (assignment.Parent is not ExpressionStatementSyntax) {
            return;
        }

        if (!IsPlainNamePath(assignment.Left) || !IsPlainNamePath(coalesce.Left)) {
            return;
        }

        if (!SyntaxFactory.AreEquivalent(assignment.Left, coalesce.Left, topLevel: false)) {
            return;
        }

        // Everything between the target and the `??` disappears. A comment in there is content.
        var deleted = TextSpan.FromBounds(assignment.Left.Span.End, coalesce.OperatorToken.Span.End);
        if (ContainsComment(assignment.SyntaxTree, deleted)) {
            return;
        }

        var fix = FixEdits.Pack((deleted, " ??="));
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(assignment.SyntaxTree, assignment.Span),
                fix,
                "Use `??=`: `" + assignment.Left + " ??= " + Trim(coalesce.Right.ToString()) + "`"
            )
        );
    }

    /// <summary>
    ///     Whether an expression is a chain of plain names — <c>x</c>, <c>this.x</c>, <c>a.b.c</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ No element access and no invocation anywhere in it. Both can have side effects and both
    ///     are evaluated a different number of times by the two forms.
    /// </remarks>
    static bool IsPlainNamePath(ExpressionSyntax expression) {
        while (true) {
            switch (expression) {
                case IdentifierNameSyntax:
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                    return true;

                case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access:
                    expression = access.Expression;
                    continue;

                default:
                    return false;
            }
        }
    }

    static bool ContainsComment(SyntaxTree tree, TextSpan span) {
        var text = tree.GetText().ToString(span);
        return text.IndexOf("//", System.StringComparison.Ordinal) >= 0
            || text.IndexOf("/*", System.StringComparison.Ordinal) >= 0;
    }

    static string Trim(string value) => value.Length <= 40 ? value : value.Substring(0, 40) + "…";
}
