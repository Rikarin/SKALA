using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Globalization;
using System.Linq;

namespace Rikarin.Skala.Rules;

/// <summary>
///     Integral expressions whose value cannot be negative, proved from a contract rather than from
///     flow.
/// </summary>
/// <remarks>
///     ⚠ Two separate proofs, kept apart on purpose. <see cref="HasNonNegativeType" /> is the
///     <em>type's</em> range, which is what <c>SK2001</c> already folds; <see cref="IsSizeExpression" />
///     is the <em>framework contract</em> that a count or a length is never below zero, which no type
///     range says. A rule that mixed them would report `someByte &gt;= 0` as a size finding and
///     duplicate <c>SK2001</c> on every unsigned operand.
/// </remarks>
internal static class NonNegativeIntegral {
    /// <summary>Whether the type itself cannot represent a negative value.</summary>
    public static bool HasNonNegativeType(ITypeSymbol? type) =>
        type?.SpecialType is SpecialType.System_Byte
            or SpecialType.System_UInt16
            or SpecialType.System_UInt32
            or SpecialType.System_UInt64
            or SpecialType.System_Char;

    /// <summary>
    ///     Whether the expression is a collection size the framework guarantees to be non-negative.
    /// </summary>
    public static bool IsSizeExpression(IOperation? operation) =>
        Unwrap(operation) switch {
            IPropertyReferenceOperation property => IsSizeProperty(property.Property),
            IInvocationOperation invocation => IsCountingCall(invocation.TargetMethod),
            _ => false
        };

    /// <summary>Every proof together: type, constant, size contract and absolute value.</summary>
    public static bool IsProvenNonNegative(IOperation? operation) {
        var value = Unwrap(operation);
        if (value is null) {
            return false;
        }

        if (HasNonNegativeType(value.Type) || IsSizeExpression(value)) {
            return true;
        }

        if (value.ConstantValue is { HasValue: true, Value: { } constant }) {
            return IsNonNegativeConstant(constant);
        }

        return value is IInvocationOperation invocation && IsAbsoluteValue(invocation.TargetMethod);
    }

    /// <summary>
    ///     ⚠ Only parentheses and <em>implicit numeric</em> conversions are stripped. Every implicit
    ///     numeric conversion in C# preserves sign, so the proof survives one; an explicit conversion
    ///     does not — <c>(int)someUlong</c> is very much able to be negative.
    /// </summary>
    /// <remarks>
    ///     ⚠ A conditional access is deliberately <em>not</em> stripped. <c>items?.Count</c> is
    ///     <c>int?</c> and can be absent rather than non-negative, so unwrapping it would hand a
    ///     caller a range the value does not have.
    /// </remarks>
    static IOperation? Unwrap(IOperation? operation) {
        while (true) {
            switch (operation) {
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    continue;
                case IConversionOperation { Conversion: { IsIdentity: false, IsNumeric: true } } conversion
                    when conversion.IsImplicit:
                    operation = conversion.Operand;
                    continue;
                default:
                    return operation;
            }
        }
    }

    static bool IsNonNegativeConstant(object constant) =>
        constant is char
        || (constant is sbyte or byte or short or ushort or int or uint or long or ulong
            && Convert.ToDecimal(constant, CultureInfo.InvariantCulture) >= 0);

    static bool IsSizeProperty(IPropertySymbol property) {
        var owner = property.ContainingType;
        return property.Name switch {
            "Length" => owner.SpecialType is SpecialType.System_Array or SpecialType.System_String
                || IsSpanLike(owner),
            "LongLength" => owner.SpecialType == SpecialType.System_Array,
            "Count" => IsCollectionContract(owner),
            _ => false
        };
    }

    static bool IsSpanLike(INamedTypeSymbol type) =>
        type is { ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }
        && type.MetadataName is "Span`1" or "ReadOnlySpan`1" or "Memory`1" or "ReadOnlyMemory`1";

    /// <summary>
    ///     ⚠ The declaring type must be a collection, not merely have a member called <c>Count</c>. A
    ///     hand-written <c>Count</c> on an unrelated type is free to return anything at all.
    /// </summary>
    static bool IsCollectionContract(INamedTypeSymbol type) =>
        IsCollectionInterface(type) || type.AllInterfaces.Any(IsCollectionInterface);

    static bool IsCollectionInterface(INamedTypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString() is "System.Collections.ICollection"
            or "System.Collections.Generic.ICollection<T>"
            or "System.Collections.Generic.IReadOnlyCollection<T>";

    static bool IsCountingCall(IMethodSymbol method) =>
        method.Name is "Count" or "LongCount"
        && method.ContainingType.ToDisplayString() == "System.Linq.Enumerable";

    /// <summary>
    ///     <c>Math.Abs</c> is non-negative for every input it returns from — the one input it cannot
    ///     handle, <c>int.MinValue</c>, throws rather than returning a negative value.
    /// </summary>
    static bool IsAbsoluteValue(IMethodSymbol method) =>
        method.Name == "Abs" && method.ContainingType.ToDisplayString() is "System.Math" or "System.MathF";
}
