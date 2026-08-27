using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

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

        // ⚠ SK1020 covers `if (x is null) throw new ArgumentNullException(nameof(x));` as one
        // finding with one fix. Reporting SK1010 on the same span would give an agent two fixes for
        // one line, and applying both in either order leaves the second one stale.
        if (ThrowIfNullAnalyzer.IsArgumentNullGuard(binary)) {
            return;
        }

        // A comment inside the comparison is content the replacement would delete.
        if (binary.SpanContainsComment()) {
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
