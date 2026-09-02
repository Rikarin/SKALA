using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2001: comparisons decided by a fixed-width integral type's entire range.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantRangeComparisonAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ConstantRangeComparison);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetOperation(binary, cancellation) is not IBinaryOperation {
                OperatorMethod: null,
                IsLifted: false
            }) {
            return;
        }

        if (!IntegralDomain.TryNormalise(model, binary, cancellation, out var value, out var kind, out var bound)) {
            return;
        }

        var type = model.GetTypeInfo(value, cancellation).Type;
        if (model.GetConstantValue(value, cancellation).HasValue
            || !IntegralDomain.TryGet(type, out var minimum, out var maximum)) {
            return;
        }

        bool? result = kind switch {
            SyntaxKind.LessThanExpression when maximum < bound => true,
            SyntaxKind.LessThanExpression when minimum >= bound => false,
            SyntaxKind.LessThanOrEqualExpression when maximum <= bound => true,
            SyntaxKind.LessThanOrEqualExpression when minimum > bound => false,
            SyntaxKind.GreaterThanExpression when minimum > bound => true,
            SyntaxKind.GreaterThanExpression when maximum <= bound => false,
            SyntaxKind.GreaterThanOrEqualExpression when minimum >= bound => true,
            SyntaxKind.GreaterThanOrEqualExpression when maximum < bound => false,
            _ => null
        };
        if (result is null
            || model.GetDiagnostics(binary.Span, cancellation).Any(static diagnostic => diagnostic.Id == "CS0652")) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                "This comparison is always "
                + (result.Value ? "true" : "false")
                + " when evaluated: the operand has type "
                + type!.ToDisplayString()
            )
        );
    }
}
