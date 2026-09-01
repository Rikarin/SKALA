using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Globalization;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

internal static class ConstantPatternSafety {
    public static ExpressionSyntax? NonNullReceiver(ExpressionSyntax expression) =>
        PatternSafety.Unwrap(expression) switch {
            BinaryExpressionSyntax comparison when comparison.IsKind(
                SyntaxKind.NotEqualsExpression
            ) => NullComparison.OperandOf(comparison),
            IsPatternExpressionSyntax {
                Pattern:
                UnaryPatternSyntax {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression }
                }
            } pattern => pattern.Expression,
            _ => null
        };

    public static bool IsSupportedType(ITypeSymbol type) =>
        PatternSafety.IsIntegral(type)
        || type.SpecialType is SpecialType.System_String or SpecialType.System_Boolean
        || type.TypeKind == TypeKind.Enum;

    public static bool IsEquality(
        SemanticModel model,
        BinaryExpressionSyntax comparison,
        CancellationToken cancellation
    ) =>
        comparison.IsKind(SyntaxKind.EqualsExpression)
        && model.GetOperation(comparison, cancellation) is IBinaryOperation { OperatorMethod: null, IsLifted: false };

    public static bool TryConstant(
        SemanticModel model,
        ExpressionSyntax expression,
        ITypeSymbol inputType,
        CancellationToken cancellation,
        out string key
    ) {
        key = string.Empty;
        // These identifier spellings acquire pattern meanings if copied verbatim into an arm.
        if (PatternSafety.Unwrap(expression) is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText is "_" or "not" or "and" or "or") {
            return false;
        }

        var constant = model.GetConstantValue(expression, cancellation);
        if (!constant.HasValue
            || !IsSupportedType(inputType)
            || !model.ClassifyConversion(expression, inputType).IsImplicit
            || !ConstantDependencies.AreFileLocal(model, expression, cancellation)) {
            return false;
        }

        if (constant.Value is null) {
            key = "null";
            return inputType.SpecialType == SpecialType.System_String;
        }

        // Normalize constants to their matched value, not their source type (1 and 1L overlap).
        key = constant.Value switch {
            string text => "string:" + text,
            bool boolean => "bool:" + boolean,
            char character => "number:" + ((int)character).ToString(CultureInfo.InvariantCulture),
            _ => "number:"
                + System.Convert.ToDecimal(constant.Value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture)
        };
        return true;
    }
}
