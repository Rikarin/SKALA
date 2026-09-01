using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2053: a comparison a count's non-negativity already decides.</summary>
/// <remarks>
///     ⚠ <b>This is not SK2001.</b> SK2001 folds a comparison the operand <em>type's</em> range
///     decides, and every count in the framework is an <c>int</c> or a <c>long</c>, whose range says
///     nothing at all about zero. The extra fact here is a framework contract — a count and a length
///     are never negative — which lowers the bound from <c>int.MinValue</c> to <c>0</c> and is the only
///     reason <c>items.Count &gt;= 0</c> is decidable. Where the type range does decide, this rule
///     stands down and leaves the finding to SK2001, so the two can never report the same expression.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonnegativeSizeComparisonAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NonnegativeSizeComparison);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ IsLifted is load-bearing rather than tidy. `list?.Count >= 0` is `false` when the
        // receiver is null, so it is not always true and reporting it would be a wrong answer.
        if (model.GetOperation(binary, cancellation) is not IBinaryOperation {
                OperatorMethod: null,
                IsLifted: false
            }) {
            return;
        }

        var size = binary.Left;
        var kind = binary.Kind();
        if (!IntegralDomain.TryConstant(model, binary.Right, cancellation, out var bound)) {
            if (!IntegralDomain.TryConstant(model, binary.Left, cancellation, out bound)) {
                return;
            }

            size = binary.Right;
            kind = Flip(kind);
        }

        if (model.GetConstantValue(size, cancellation).HasValue
            || !NonNegativeIntegral.IsSizeExpression(model.GetOperation(size, cancellation))
            || !IntegralDomain.TryGet(model.GetTypeInfo(size, cancellation).Type, out var minimum, out var maximum)) {
            return;
        }

        // SK2001's answer first. If the type's own range already settles it, this rule has nothing
        // to add and must not double-report.
        if (Decide(kind, minimum, maximum, bound) is not null) {
            return;
        }

        if (Decide(kind, 0, maximum, bound) is not { } result) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                "This comparison is always "
                + (result ? "true" : "false")
                + ": '"
                + size
                + "' is never negative"
            )
        );
    }

    static SyntaxKind Flip(SyntaxKind kind) =>
        kind switch {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind
        };

    static bool? Decide(SyntaxKind kind, decimal minimum, decimal maximum, decimal bound) =>
        kind switch {
            SyntaxKind.LessThanExpression when maximum < bound => true,
            SyntaxKind.LessThanExpression when minimum >= bound => false,
            SyntaxKind.LessThanOrEqualExpression when maximum <= bound => true,
            SyntaxKind.LessThanOrEqualExpression when minimum > bound => false,
            SyntaxKind.GreaterThanExpression when minimum > bound => true,
            SyntaxKind.GreaterThanExpression when maximum <= bound => false,
            SyntaxKind.GreaterThanOrEqualExpression when minimum >= bound => true,
            SyntaxKind.GreaterThanOrEqualExpression when maximum < bound => false,
            SyntaxKind.EqualsExpression when bound < minimum || bound > maximum => false,
            SyntaxKind.NotEqualsExpression when bound < minimum || bound > maximum => true,
            _ => null
        };
}
