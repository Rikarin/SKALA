using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1014: two integral comparisons on stable storage can share a pattern input.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RelationalPatternAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RelationalPattern);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "9.0")) {
                    start.RegisterSyntaxNodeAction(
                        Analyze,
                        SyntaxKind.LogicalAndExpression,
                        SyntaxKind.LogicalOrExpression
                    );
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (PatternSafety.Unwrap(binary.Left) is not BinaryExpressionSyntax left
            || PatternSafety.Unwrap(binary.Right) is not BinaryExpressionSyntax right
            || !IsRelational(left)
            || !IsRelational(right)
            || !PatternSafety.CanRewrite(context.SemanticModel, binary, context.CancellationToken)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var operand = PatternSafety.Unwrap(left.Left);
        var symbol = PatternSafety.StableVariable(model, operand, cancellation);
        var type = model.GetTypeInfo(operand, cancellation).Type;
        if (symbol is null
            || !PatternSafety.IsIntegral(type)
            || !SymbolEqualityComparer.Default.Equals(
                symbol,
                model.GetSymbolInfo(PatternSafety.Unwrap(right.Left), cancellation).Symbol
            )
            || model.GetOperation(left, cancellation) is not IBinaryOperation { OperatorMethod: null }
            || model.GetOperation(right, cancellation) is not IBinaryOperation { OperatorMethod: null }
            || !model.GetConstantValue(left.Right, cancellation).HasValue
            || !model.GetConstantValue(right.Right, cancellation).HasValue
            || !model.ClassifyConversion(left.Right, type!).IsImplicit
            || !model.ClassifyConversion(right.Right, type!).IsImplicit
            || !HasVariableResult(binary, left, right, type!, model)) {
            return;
        }

        var join = binary.IsKind(SyntaxKind.LogicalAndExpression) ? "and" : "or";
        var replacement = "("
            + operand
            + " is "
            + left.OperatorToken.Text
            + " "
            + left.Right
            + " "
            + join
            + " "
            + right.OperatorToken.Text
            + " "
            + right.Right
            + ")";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.GetLocation(),
                FixEdits.Pack((binary.Span, replacement)),
                "Combine the comparisons with a relational/logical pattern"
            )
        );
    }

    static bool IsRelational(BinaryExpressionSyntax expression) =>
        expression.Kind() is
        SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression;

    // An impossible pattern is a compiler error even when the original boolean expression is legal.
    // Use exact integral intervals (decimal represents every Int64/UInt64 value exactly).
    static bool HasVariableResult(
        BinaryExpressionSyntax binary,
        BinaryExpressionSyntax left,
        BinaryExpressionSyntax right,
        ITypeSymbol type,
        SemanticModel model
    ) {
        var (minimum, maximum) = type.SpecialType switch {
            SpecialType.System_SByte => ((decimal)sbyte.MinValue, (decimal)sbyte.MaxValue),
            SpecialType.System_Byte => (byte.MinValue, (decimal)byte.MaxValue),
            SpecialType.System_Int16 => (short.MinValue, (decimal)short.MaxValue),
            SpecialType.System_UInt16 or SpecialType.System_Char => (ushort.MinValue, (decimal)ushort.MaxValue),
            SpecialType.System_Int32 => (int.MinValue, (decimal)int.MaxValue),
            SpecialType.System_UInt32 => (uint.MinValue, (decimal)uint.MaxValue),
            SpecialType.System_Int64 => (long.MinValue, (decimal)long.MaxValue),
            _ => (ulong.MinValue, (decimal)ulong.MaxValue)
        };
        var a = Interval(left, model, minimum, maximum);
        var b = Interval(right, model, minimum, maximum);
        if (a.Low > a.High || b.Low > b.High) {
            return false;
        }

        if (binary.IsKind(SyntaxKind.LogicalAndExpression)) {
            var low = Math.Max(a.Low, b.Low);
            var high = Math.Min(a.High, b.High);
            return low <= high && (low > minimum || high < maximum);
        }

        return !(Math.Min(a.Low, b.Low) == minimum
            && Math.Max(a.High, b.High) == maximum
            && Math.Max(a.Low, b.Low) <= Math.Min(a.High, b.High) + 1);
    }

    static (decimal Low, decimal High) Interval(
        BinaryExpressionSyntax comparison,
        SemanticModel model,
        decimal minimum,
        decimal maximum
    ) {
        var constant = model.GetConstantValue(comparison.Right).Value;
        var value = constant is char character
            ? character
            : Convert.ToDecimal(constant, System.Globalization.CultureInfo.InvariantCulture);
        return comparison.Kind() switch {
            SyntaxKind.LessThanExpression => (minimum, Math.Min(maximum, value - 1)),
            SyntaxKind.LessThanOrEqualExpression => (minimum, Math.Min(maximum, value)),
            SyntaxKind.GreaterThanExpression => (Math.Max(minimum, value + 1), maximum),
            _ => (Math.Max(minimum, value), maximum)
        };
    }
}
