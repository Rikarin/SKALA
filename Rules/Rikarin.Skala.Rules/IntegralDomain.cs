using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Globalization;
using System.Threading;

namespace Rikarin.Skala.Rules;

/// <summary>Exact, target-independent ranges for the fixed-width integral types.</summary>
internal static class IntegralDomain {
    public static bool TryGet(ITypeSymbol? type, out decimal minimum, out decimal maximum) {
        (minimum, maximum) = type?.SpecialType switch {
            SpecialType.System_SByte => ((decimal)sbyte.MinValue, (decimal)sbyte.MaxValue),
            SpecialType.System_Byte => (byte.MinValue, (decimal)byte.MaxValue),
            SpecialType.System_Int16 => (short.MinValue, (decimal)short.MaxValue),
            SpecialType.System_UInt16 or SpecialType.System_Char => (ushort.MinValue, (decimal)ushort.MaxValue),
            SpecialType.System_Int32 => (int.MinValue, (decimal)int.MaxValue),
            SpecialType.System_UInt32 => (uint.MinValue, (decimal)uint.MaxValue),
            SpecialType.System_Int64 => (long.MinValue, (decimal)long.MaxValue),
            SpecialType.System_UInt64 => (ulong.MinValue, (decimal)ulong.MaxValue),
            _ => (1, 0)
        };
        return minimum <= maximum;
    }

    public static bool TryConstant(
        SemanticModel model,
        ExpressionSyntax expression,
        CancellationToken cancellation,
        out decimal value
    ) {
        value = 0;
        var constant = model.GetConstantValue(expression, cancellation);
        if (!constant.HasValue
            || !TryGet(model.GetTypeInfo(expression, cancellation).Type, out _, out _)
            || !ConstantDependencies.AreFileLocal(model, expression, cancellation)) {
            return false;
        }

        value = constant.Value is char character
            ? character
            : Convert.ToDecimal(constant.Value, CultureInfo.InvariantCulture);
        return true;
    }
}
