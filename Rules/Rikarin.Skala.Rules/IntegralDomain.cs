using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    /// <summary>
    ///     Reads a comparison as <c>subject &lt;op&gt; constant</c>, whichever side the author wrote
    ///     the constant on.
    /// </summary>
    /// <remarks>
    ///     ⚠ Shared rather than duplicated. <c>SK2001</c> and <c>SK2002</c> both need the constant on
    ///     the right before their tables mean anything, and both wrote this out — one with a
    ///     <c>Flip</c> helper and one with the same switch inlined. The flip is the part that must not
    ///     drift: getting one arm wrong turns <c>0 &lt; x</c> into <c>x &lt; 0</c> and inverts the
    ///     rule's answer rather than losing it, which is the failure that reports a correct comparison
    ///     as always-true.
    ///     <para>
    ///         A comparison with a constant on <b>both</b> sides normalises to the right-hand one, which
    ///         is what both callers did before; each rejects it afterwards by asking whether the
    ///         subject is itself constant.
    ///     </para>
    /// </remarks>
    public static bool TryNormalise(
        SemanticModel model,
        BinaryExpressionSyntax binary,
        CancellationToken cancellation,
        out ExpressionSyntax subject,
        out SyntaxKind kind,
        out decimal bound
    ) {
        subject = binary.Left;
        kind = binary.Kind();
        if (TryConstant(model, binary.Right, cancellation, out bound)) {
            return true;
        }

        if (!TryConstant(model, binary.Left, cancellation, out bound)) {
            return false;
        }

        subject = binary.Right;
        kind = Flip(kind);
        return true;
    }

    /// <summary>The operator that means the same thing with the operands swapped.</summary>
    /// <remarks>
    ///     ⚠ <c>==</c> and <c>!=</c> pass through unchanged, because they already do. Only
    ///     <c>SK2002</c> registers them; returning anything else here would be wrong for it and
    ///     unreachable for <c>SK2001</c>, which is exactly the shape a duplicated copy gets wrong.
    /// </remarks>
    public static SyntaxKind Flip(SyntaxKind kind) =>
        kind switch {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind
        };
}
