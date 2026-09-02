using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;

namespace Rikarin.Skala.Rules.Security;

/// <summary>How a byte expression was written, when it was written as a constant.</summary>
public enum ConstantByteKind {
    /// <summary>Not decidably constant — a local, a parameter, a call, anything followed.</summary>
    NotConstant,

    /// <summary><c>new byte[16]</c>: allocated and never filled at this expression.</summary>
    ZeroArray,

    /// <summary><c>new byte[] { 1, 2, … }</c>, <c>[1, 2, …]</c>.</summary>
    LiteralList,

    /// <summary><c>Encoding.UTF8.GetBytes("…")</c>, and <c>"…"u8</c>.</summary>
    LiteralStringBytes,

    /// <summary><c>Convert.FromBase64String("…")</c>, <c>Convert.FromHexString("…")</c>.</summary>
    DecodedLiteral,

    /// <summary><c>static readonly byte[] Salt = { 1, 2, … };</c>.</summary>
    ConstantField
}

/// <summary>
///     Whether a <c>byte[]</c>-shaped expression's value is fixed at compile time.
/// </summary>
/// <remarks>
///     ⚠ <b>Nothing here resolves a local, and a false positive is the reason rather than cost.</b>
///     <c>var salt = new byte[16]; RandomNumberGenerator.Fill(salt); …</c> is how correct code is
///     written, and a rule that followed <c>salt</c> to its declaration would report it. So a constant
///     must be written <em>at the expression itself</em>, or be the initialiser of a field holding an
///     explicit list of literals — <c>static readonly byte[] Salt = { 1, 2, … }</c> is a hard-coded value
///     and cannot be anything else, while <c>= new byte[16]</c> is the allocate-then-fill shape and is
///     deliberately not followed.
///     <para>
///         ⚠ Shared by <c>SK5020</c> (a cipher's initialisation vector) and <c>SK5041</c> (a key
///         derivation's salt). The two rules ask the same question of a different argument, and the
///         answer must not be able to differ between them: an expression that is a constant IV is a
///         constant salt, and a change to what counts must move both or neither.
///     </para>
/// </remarks>
public static class ConstantBytes {
    /// <summary>Classifies an expression, or returns <see cref="ConstantByteKind.NotConstant" />.</summary>
    /// <param name="value">The argument or assigned value, conversions and parentheses included.</param>
    /// <param name="encoding"><c>System.Text.Encoding</c>, or <c>null</c> if not in the compilation.</param>
    /// <param name="convert"><c>System.Convert</c>, or <c>null</c> if not in the compilation.</param>
    /// <param name="fieldName">
    ///     The field's name when the answer is <see cref="ConstantByteKind.ConstantField" />.
    /// </param>
    public static ConstantByteKind Classify(
        IOperation value,
        INamedTypeSymbol? encoding,
        INamedTypeSymbol? convert,
        out string? fieldName
    ) {
        fieldName = null;
        var operation = Unwrap(value);

        // ⚠ On syntax, because `[1, 2, …]` lowers to an operation kind this would otherwise have to
        // name to match, and naming it pins the Roslyn version the rules compile against.
        if (operation.Syntax is CollectionExpressionSyntax collection && AllLiterals(collection)) {
            return ConstantByteKind.LiteralList;
        }

        // `"salt"u8` — a UTF-8 literal is a `ReadOnlySpan<byte>` of fixed content, and it is the
        // shortest way to write a hard-coded salt in modern C#.
        if (operation.Syntax is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.Utf8StringLiteralExpression)) {
            return ConstantByteKind.LiteralStringBytes;
        }

        switch (operation) {
            case IArrayCreationOperation { Initializer: null }:
                return ConstantByteKind.ZeroArray;

            case IArrayCreationOperation { Initializer: { } initializer }
                when initializer.ElementValues.All(static element => Unwrap(element).ConstantValue.HasValue):
                return ConstantByteKind.LiteralList;

            case IInvocationOperation invocation:
                return FromCall(invocation, encoding, convert);

            case IFieldReferenceOperation { Field: { IsStatic: true } field } when ConstantArrayField(field):
                fieldName = field.Name;
                return ConstantByteKind.ConstantField;

            default:
                return ConstantByteKind.NotConstant;
        }
    }

    static ConstantByteKind FromCall(
        IInvocationOperation invocation,
        INamedTypeSymbol? encoding,
        INamedTypeSymbol? convert
    ) {
        if (invocation.Arguments.Length != 1 || !Unwrap(invocation.Arguments[0].Value).ConstantValue.HasValue) {
            return ConstantByteKind.NotConstant;
        }

        var containing = invocation.TargetMethod.ContainingType;
        if (invocation.TargetMethod.Name == "GetBytes" && Inherits(containing, encoding)) {
            return ConstantByteKind.LiteralStringBytes;
        }

        return SymbolEqualityComparer.Default.Equals(containing, convert)
            && (invocation.TargetMethod.Name == "FromBase64String" || invocation.TargetMethod.Name == "FromHexString")
                ? ConstantByteKind.DecodedLiteral
                : ConstantByteKind.NotConstant;
    }

    /// <summary>
    ///     Whether a field is declared with an explicit, non-empty list of literal elements.
    /// </summary>
    /// <remarks>
    ///     ⚠ Decided on syntax, and the list must be non-empty. A field initialised <c>= new byte[16]</c>
    ///     is the allocate-then-fill shape and a static constructor may well fill it;
    ///     <c>= { 1, 2, 3 }</c> cannot be anything but a hard-coded value.
    /// </remarks>
    public static bool ConstantArrayField(IFieldSymbol field) {
        foreach (var reference in field.DeclaringSyntaxReferences) {
            if (reference.GetSyntax() is not VariableDeclaratorSyntax { Initializer.Value: { } initializer }) {
                continue;
            }

            var elements = initializer switch {
                ArrayCreationExpressionSyntax { Initializer: { } list } => list.Expressions.Count,
                ImplicitArrayCreationExpressionSyntax implicitly => implicitly.Initializer.Expressions.Count,
                InitializerExpressionSyntax braces => braces.Expressions.Count,
                CollectionExpressionSyntax collection => collection.Elements.Count,
                _ => 0
            };

            if (elements > 0 && AllLiterals(initializer)) {
                return true;
            }
        }

        return false;
    }

    public static bool AllLiterals(ExpressionSyntax initializer) {
        var expressions = initializer switch {
            ArrayCreationExpressionSyntax { Initializer: { } list } => list.Expressions.ToArray(),
            ImplicitArrayCreationExpressionSyntax implicitly => implicitly.Initializer.Expressions.ToArray(),
            InitializerExpressionSyntax braces => braces.Expressions.ToArray(),
            CollectionExpressionSyntax collection => collection.Elements
                .OfType<ExpressionElementSyntax>()
                .Select(static element => element.Expression)
                .ToArray(),
            _ => System.Array.Empty<ExpressionSyntax>()
        };

        return expressions.Length > 0
            && expressions.All(static expression =>
                expression is LiteralExpressionSyntax
                    or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax }
            );
    }

    public static IOperation Unwrap(IOperation operation) {
        var current = operation;
        while (true) {
            switch (current) {
                case IConversionOperation conversion:
                    current = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    current = parenthesized.Operand;
                    continue;
                default:
                    return current;
            }
        }
    }

    public static bool Inherits(ITypeSymbol? type, INamedTypeSymbol? ancestor) {
        if (ancestor is null) {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, ancestor)) {
                return true;
            }
        }

        return false;
    }
}
