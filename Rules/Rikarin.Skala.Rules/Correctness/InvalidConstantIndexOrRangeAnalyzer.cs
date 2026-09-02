using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2210</c> — a constant index or range no length can make valid.
/// </summary>
/// <remarks>
///     Three shapes, and what they have in common is that the length never enters the arithmetic.
///     <c>x[^0]</c> is <c>Length - 0</c>, one past the end, and throws on a full collection and an
///     empty one alike; a range whose start is fixed above its end is rejected by
///     <c>Range.GetOffsetAndLength</c> before any length is consulted; and a negative constant index
///     throws from every positional indexer.
///     <para>
///         ⚠ <b>This is neither <c>SK2001</c> nor <c>SK2053</c>.</b> Those fold a <em>comparison</em>
///         that the operand type's range, or a count's non-negativity, already decides; neither looks at
///         an element access, and no type range or count contract says anything about <c>^0</c>. The
///         fact this rule adds is the indexing contract itself — what offset <c>^0</c> denotes, the
///         ordering <c>Range</c> requires of its endpoints, and the non-negativity every positional
///         indexer requires — each provable from the constants on the page with no knowledge of the
///         length at all.
///     </para>
///     <para>
///         ⚠ <b><c>^0</c> is reported only where it indexes an element, never where it bounds a
///         range.</b> <c>x[..^0]</c> is the whole collection and <c>x[^0..]</c> is an empty slice; both
///         are legal, measured so on an empty collection too, and both are spellings people choose
///         deliberately.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidConstantIndexOrRangeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InvalidConstantIndexOrRange);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ElementAccessExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var access = (ElementAccessExpressionSyntax)context.Node;
        if (access.ArgumentList.Arguments.Count != 1) {
            return;
        }

        var argument = access.ArgumentList.Arguments[0].Expression;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (Reason(model, access, argument, cancellation) is not { } reason) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, argument.GetLocation(), reason));
    }

    static string? Reason(
        SemanticModel model,
        ElementAccessExpressionSyntax access,
        ExpressionSyntax argument,
        CancellationToken cancellation
    ) =>
        argument switch {
            RangeExpressionSyntax range when IsLanguageSliced(model, access, cancellation)
                => ReversedRange(model, range, cancellation),
            RangeExpressionSyntax => null,
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.IndexExpression } index
                => ZeroFromEnd(model, access, index, cancellation),
            _ => NegativeIndex(model, access, argument, cancellation)
        };

    /// <summary>
    ///     ⚠ <c>^0</c> is the last element only if you read it as a count. It is an offset, and the
    ///     offset it names is <c>Length</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The indexer the access resolves to must take an <c>int</c>, which is the compiler lowering
    ///     <c>^0</c> through the <c>Length</c>/<c>Count</c> pattern to a position. A type that declares
    ///     its own <c>this[Index]</c> receives the <c>Index</c> value whole and may give it any meaning
    ///     it likes, so it is declined.
    /// </remarks>
    static string? ZeroFromEnd(
        SemanticModel model,
        ElementAccessExpressionSyntax access,
        PrefixUnaryExpressionSyntax index,
        CancellationToken cancellation
    ) {
        if (model.GetConstantValue(index.Operand, cancellation) is not { HasValue: true, Value: 0 }
            || model.GetTypeInfo(index, cancellation).Type?.ToDisplayString() != "System.Index"
            || !TakesAnInteger(model, access, cancellation)) {
            return null;
        }

        return "'^0' is one past the end — it is 'Length - 0', not the last element, and throws for every length";
    }

    /// <summary>Whether the access resolves to an array, or to an indexer taking a single int.</summary>
    /// <remarks>
    ///     ⚠ Spelled out rather than with a list pattern: this assembly targets <c>netstandard2.0</c>,
    ///     which has no <c>System.Index</c>, and a list pattern needs one to lower.
    /// </remarks>
    static bool TakesAnInteger(SemanticModel model, ElementAccessExpressionSyntax access, CancellationToken cancellation) {
        var symbol = model.GetSymbolInfo(access, cancellation).Symbol;
        if (symbol is null) {
            return model.GetTypeInfo(access.Expression, cancellation).Type is IArrayTypeSymbol;
        }

        return symbol is IPropertySymbol indexer
            && indexer.Parameters.Length == 1
            && indexer.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    }

    /// <summary>
    ///     ⚠ Only the receivers whose <c>Range</c> semantics are the language's own. A type declaring
    ///     its own <c>this[Range]</c> is free to interpret a reversed range however it wishes.
    /// </summary>
    static bool IsLanguageSliced(SemanticModel model, ElementAccessExpressionSyntax access, CancellationToken cancellation) {
        var receiver = model.GetTypeInfo(access.Expression, cancellation).Type;
        return receiver is IArrayTypeSymbol
            || receiver?.SpecialType == SpecialType.System_String
            || (receiver is INamedTypeSymbol named && IsSpanLike(named));
    }

    /// <summary>
    ///     A range whose endpoints are constants of the same kind, ordered so that no length can make
    ///     the start reach the end.
    /// </summary>
    /// <remarks>
    ///     ⚠ Same kind, because a mixed <c>3..^1</c> depends on the length and is not decidable here.
    ///     <c>^a..^b</c> compares the other way round: the start is <c>Length - a</c> and the end is
    ///     <c>Length - b</c>, so it is invalid exactly when <c>b &gt; a</c>.
    /// </remarks>
    static string? ReversedRange(SemanticModel model, RangeExpressionSyntax range, CancellationToken cancellation) {
        if (range.LeftOperand is not { } left || range.RightOperand is not { } right) {
            return null;
        }

        var startFromEnd = left.IsKind(SyntaxKind.IndexExpression);
        if (startFromEnd != right.IsKind(SyntaxKind.IndexExpression)
            || Offset(model, left, cancellation) is not { } start
            || Offset(model, right, cancellation) is not { } end) {
            return null;
        }

        var invalid = startFromEnd ? end > start : start > end;
        return invalid
            ? "'" + range + "' starts after it ends, so 'Range.GetOffsetAndLength' throws for every length"
            : null;
    }

    static int? Offset(SemanticModel model, ExpressionSyntax endpoint, CancellationToken cancellation) {
        var value = endpoint is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.IndexExpression } index
            ? index.Operand
            : endpoint;
        return model.GetConstantValue(value, cancellation) is { HasValue: true, Value: int constant } && constant >= 0
            ? constant
            : null;
    }

    /// <summary>
    ///     ⚠ Arrays are excluded because <c>CS0251</c> already owns them — measured, not assumed. The
    ///     compiler reports <c>a[-1]</c> on an array and stays silent on <c>"abc"[-1]</c>,
    ///     <c>list[-1]</c> and <c>span[-1]</c>, which all throw at run time.
    /// </summary>
    static string? NegativeIndex(
        SemanticModel model,
        ElementAccessExpressionSyntax access,
        ExpressionSyntax argument,
        CancellationToken cancellation
    ) {
        if (model.GetConstantValue(argument, cancellation) is not { HasValue: true, Value: int constant }
            || constant >= 0
            || model.GetTypeInfo(access.Expression, cancellation).Type is not { } receiver
            || !IsPositionallyIndexed(receiver)) {
            return null;
        }

        return "a negative index throws from every positional indexer; '" + receiver.Name + "' is one";
    }

    /// <summary>
    ///     Receivers whose <c>int</c> indexer is a position rather than a key.
    /// </summary>
    /// <remarks>
    ///     ⚠ A dictionary is excluded outright: <c>map[-1]</c> is an ordinary key lookup and not an
    ///     out-of-range access. A user-defined indexer outside these contracts is free to give a
    ///     negative argument any meaning it likes.
    /// </remarks>
    static bool IsPositionallyIndexed(ITypeSymbol receiver) {
        if (receiver.SpecialType == SpecialType.System_String) {
            return true;
        }

        if (receiver is not INamedTypeSymbol named || named.AllInterfaces.Any(IsKeyed) || IsKeyed(named)) {
            return false;
        }

        return IsSpanLike(named) || named.AllInterfaces.Any(IsPositional) || IsPositional(named);
    }

    static bool IsSpanLike(INamedTypeSymbol type) =>
        type is { ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }
        && type.MetadataName is "Span`1" or "ReadOnlySpan`1" or "Memory`1" or "ReadOnlyMemory`1";

    static bool IsPositional(INamedTypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString() is "System.Collections.IList"
            or "System.Collections.Generic.IList<T>"
            or "System.Collections.Generic.IReadOnlyList<T>";

    static bool IsKeyed(INamedTypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString() is "System.Collections.IDictionary"
            or "System.Collections.Generic.IDictionary<TKey, TValue>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>";
}
