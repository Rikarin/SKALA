using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2003: exact equality involving a directly computed floating-point result.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FloatingPointEqualityAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FloatingPointEquality);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetOperation(binary, cancellation) is not IBinaryOperation {
                OperatorMethod: null,
                IsLifted: false
            } operation
            || !IsFloating(operation.LeftOperand.Type)
            || !IsFloating(operation.RightOperand.Type)
            || operation.ConstantValue.HasValue
            || !IsArithmetic(model.GetOperation(PatternSafety.Unwrap(binary.Left), cancellation))
            && !IsArithmetic(model.GetOperation(PatternSafety.Unwrap(binary.Right), cancellation))
            || !ConstantDependencies.AreFileLocal(model, binary, cancellation)
            || IsSentinel(model.GetConstantValue(binary.Left, cancellation))
            || IsSentinel(model.GetConstantValue(binary.Right, cancellation))) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                "Exact equality of a floating-point arithmetic result can expose rounding; review the intended comparison"
            )
        );
    }

    static bool IsArithmetic(IOperation? operation) =>
        operation is IBinaryOperation {
            OperatorMethod: null,
            IsLifted: false,
            OperatorKind:
            BinaryOperatorKind.Add
                or BinaryOperatorKind.Subtract
                or BinaryOperatorKind.Multiply
                or BinaryOperatorKind.Divide
                or BinaryOperatorKind.Remainder
        }
        && IsFloating(operation.Type);

    static bool IsFloating(ITypeSymbol? type) =>
        type?.SpecialType is SpecialType.System_Single or SpecialType.System_Double;

    static bool IsSentinel(Optional<object?> constant) =>
        constant.HasValue
        && constant.Value switch {
            double value => value == 0 || double.IsNaN(value) || double.IsInfinity(value),
            float value => value == 0 || float.IsNaN(value) || float.IsInfinity(value),
            char value => value == 0,
            sbyte or byte or short or ushort or int or uint or long or ulong => System.Convert.ToDecimal(
                constant.Value,
                System.Globalization.CultureInfo.InvariantCulture
            )
                == 0,
            _ => false
        };
}
