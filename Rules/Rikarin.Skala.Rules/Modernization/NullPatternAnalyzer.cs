using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1010</c> — <c>x != null</c> is <c>x is not null</c>, on a type that defines no operator.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "Pattern matching". The guard is
///     <see cref="NullComparison.IsRewritable" /> and it is the rule.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullPatternAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.IsNullPattern);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IsNullPattern);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    Analyze,
                    SyntaxKind.EqualsExpression,
                    SyntaxKind.NotEqualsExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var operand = NullComparison.OperandOf(binary);
        if (operand is null) {
            return;
        }

        // ⚠ This used to yield an argument-null guard to SK1020, which owned
        // `if (x == null) throw new ArgumentNullException(nameof(x));` as one finding with one fix.
        // SK1020 is retired (#281 — CA1510 reports all three of its positive fixtures at `note` in a
        // stock build), so there is no second finding to collide with and nothing left to yield to.
        // Keeping the yield would have left SK1010 silent on a shape it owns, for the sake of a rule
        // that no longer exists — a silence with no live reason is indistinguishable from a bug.

        // A comment inside the comparison is content the replacement would delete.
        if (RewriteGuards.ContainsCommentOrDirective(binary.SyntaxTree, binary.Span)) {
            return;
        }

        var cancellation = context.CancellationToken;
        if (!NullComparison.IsRewritable(context.SemanticModel, operand, cancellation)
            || NullComparison.InsideExpressionTree(context.SemanticModel, binary, cancellation)) {
            return;
        }

        var negated = binary.IsKind(SyntaxKind.NotEqualsExpression);
        var pattern = negated ? "is not null" : "is null";
        var replacement = operand + " " + pattern;

        var fix = FixEdits.Pack((binary.Span, replacement));
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(binary.SyntaxTree, binary.Span),
                fix,
                "Use `" + pattern + "` instead of `" + binary.OperatorToken.ValueText + " null`"
            )
        );
    }
}
