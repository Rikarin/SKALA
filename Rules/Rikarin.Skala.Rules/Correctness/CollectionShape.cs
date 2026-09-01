using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Rules.Performance;
using System;
using System.Globalization;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The two questions the collection rules in this range keep having to ask: is this key a
///     constant this analyzer can compare, and are these two expressions the same storage.
/// </summary>
/// <remarks>
///     ⚠ Both are places a rule goes quietly wrong rather than loudly, which is why they are here
///     once instead of three times. A key comparison that reads the boxed constant instead of its
///     value misses <c>1</c> against <c>1L</c>; a storage comparison that matches member symbols
///     without walking the receivers says <c>a.Items</c> and <c>b.Items</c> are one object.
/// </remarks>
internal static class CollectionShape {
    /// <summary>
    ///     A constant key as a comparable string, or null when the expression is not a constant of a
    ///     type whose default equality this range decides.
    /// </summary>
    /// <remarks>
    ///     ⚠ The numeric constants are normalised to one spelling on purpose. In a
    ///     <c>Dictionary&lt;long, V&gt;</c> the keys <c>1</c> and <c>1L</c> are boxed as <c>int</c>
    ///     and <c>long</c>, and comparing the boxes says they differ where the dictionary says they
    ///     are one key.
    ///     <para>
    ///         ⚠ <c>double</c>, <c>float</c> and <c>decimal</c> are absent even though their boxed
    ///         <c>Equals</c> agrees with <c>EqualityComparer&lt;T&gt;.Default</c>: a rule arguing about
    ///         a duplicate <c>NaN</c> or <c>-0.0</c> is arguing about a key nobody writes.
    ///     </para>
    /// </remarks>
    internal static string? ConstantKey(SemanticModel model, ExpressionSyntax key, CancellationToken cancellation) {
        var constant = model.GetConstantValue(key, cancellation);
        if (!constant.HasValue) {
            return null;
        }

        return constant.Value switch {
            null => "null",
            string text => "s:" + text,
            bool flag => flag ? "b:1" : "b:0",
            char character => "n:" + ((long)character).ToString(CultureInfo.InvariantCulture),
            ulong unsigned => "n:" + unsigned.ToString(CultureInfo.InvariantCulture),
            sbyte or byte or short or ushort or int or uint or long =>
                "n:"
                + Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    /// <summary>
    ///     Whether a key type is one whose default equality this range decides from constants alone.
    /// </summary>
    internal static bool IsDecidableKeyType(ITypeSymbol keyType) =>
        keyType.TypeKind == TypeKind.Enum
        || keyType.SpecialType is SpecialType.System_String
            or SpecialType.System_Boolean
            or SpecialType.System_Char
            or SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64;

    /// <summary>
    ///     Whether two expressions certainly denote the same object, by walking both name paths and
    ///     comparing symbols.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every symbol on the path has to be a local, a parameter or a field. A property is an
    ///     accessor call, and two reads of one property may hand back two different objects; an
    ///     invocation or an indexer anywhere in the path is excluded by <c>IsPlainNamePath</c> for the
    ///     same reason. ⚠ The receivers are walked, not just the last member: <c>a.Items</c> and
    ///     <c>b.Items</c> resolve to one field symbol through two different objects.
    /// </remarks>
    internal static bool SameStorage(
        SemanticModel model,
        ExpressionSyntax left,
        ExpressionSyntax right,
        CancellationToken cancellation
    ) {
        left = Unwrap(left);
        right = Unwrap(right);
        if (!CallShape.IsPlainNamePath(left) || !CallShape.IsPlainNamePath(right)) {
            return false;
        }

        while (true) {
            if (left is ThisExpressionSyntax && right is ThisExpressionSyntax) {
                return true;
            }

            if (left is BaseExpressionSyntax && right is BaseExpressionSyntax) {
                return true;
            }

            var leftSymbol = model.GetSymbolInfo(left, cancellation).Symbol;
            var rightSymbol = model.GetSymbolInfo(right, cancellation).Symbol;
            if (leftSymbol is not (ILocalSymbol or IParameterSymbol or IFieldSymbol)
                || !SymbolEqualityComparer.Default.Equals(leftSymbol, rightSymbol)) {
                return false;
            }

            var leftNext = Receiver(left);
            var rightNext = Receiver(right);

            // `items` and `this.items` are one storage written two ways, and the symbol above has
            // already established it is the same member of the same type.
            if (leftNext is null) {
                return rightNext is null or ThisExpressionSyntax;
            }

            if (rightNext is null) {
                return leftNext is ThisExpressionSyntax;
            }

            left = leftNext;
            right = rightNext;
        }
    }

    static ExpressionSyntax? Receiver(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax access ? Unwrap(access.Expression) : null;

    internal static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parentheses) {
            expression = parentheses.Expression;
        }

        return expression;
    }
}
