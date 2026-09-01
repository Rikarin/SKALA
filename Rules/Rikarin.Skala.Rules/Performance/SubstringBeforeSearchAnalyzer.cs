using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4032</c> — <c>s.Substring(n).IndexOf(x)</c> allocates a string to search inside.
/// </summary>
/// <remarks>
///     <para>
///         <c>Substring</c> copies the tail of the string onto the heap, and the only thing done with
///         the copy is a search that already takes the offset as an argument. <c>s.IndexOf(x, n)</c>
///         reads the same characters in place. <c>SK1028</c> makes the span-based version of this
///         argument; this one needs no new type at the call site at all.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The two expressions do not return the same number, and that is the whole difficulty of
///             this rule.
///         </b> <c>s.Substring(n).IndexOf(x)</c> is an index into the copy and
///         <c>s.IndexOf(x, n)</c> is an index into <c>s</c>; they differ by exactly <c>n</c>. So the
///         rewrite is offered only where the result is being used as a <em>presence test</em> —
///         compared with <c>0</c> or <c>-1</c> in a way that asks "found or not" — because that is the
///         one question both spellings answer identically. Anywhere the number itself is kept, the
///         rule is silent, and a fix there would have been a silently wrong offset.
///     </para>
///     <para>
///         ⚠ <c>LastIndexOf</c> is excluded rather than overlooked. Its <c>startIndex</c> overload
///         searches <em>backwards</em> from that position, so the substituted call is not a narrower
///         search — it is the opposite search.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SubstringBeforeSearchAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SubstringBeforeIndexSearch);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var text = start.Compilation.GetSpecialType(SpecialType.System_String);
                var integer = start.Compilation.GetSpecialType(SpecialType.System_Int32);
                if (text.TypeKind == TypeKind.Error || integer.TypeKind == TypeKind.Error) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, text, integer),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol text, INamedTypeSymbol integer) {
        var search = (InvocationExpressionSyntax)context.Node;
        if (search.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } searchAccess
            || searchAccess.Name.Identifier.ValueText is not ("IndexOf" or "IndexOfAny")
            || searchAccess.Expression is not InvocationExpressionSyntax substring
            || substring.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Name.Identifier.ValueText: "Substring"
            } substringAccess) {
            return;
        }

        // ⚠ The one-argument overload only. `Substring(n, length)` bounds the search as well as
        // moving its start, and `IndexOf(x, n)` would look past the end of what was asked for.
        if (substring.ArgumentList.Arguments.Count != 1
            || substring.ArgumentList.Arguments[0] is not { NameColon: null } offset) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetSymbolInfo(substring, cancellation).Symbol is not IMethodSymbol {
                Parameters.Length: 1
            } substringMethod
            || !SymbolEqualityComparer.Default.Equals(substringMethod.ContainingType, text)
            || substringMethod.Parameters[0].Type.SpecialType != SpecialType.System_Int32) {
            return;
        }

        if (model.GetSymbolInfo(search, cancellation).Symbol is not IMethodSymbol searchMethod
            || !SymbolEqualityComparer.Default.Equals(searchMethod.ContainingType, text)
            || searchMethod.Parameters.Length == 0
            || search.ArgumentList.Arguments.Count != searchMethod.Parameters.Length) {
            return;
        }

        // ⚠ The offset is inserted as the second positional argument, so a named argument anywhere
        // in the list makes the written order and the parameter order two different things.
        foreach (var argument in search.ArgumentList.Arguments) {
            if (argument.NameColon is not null || !IsSideEffectFree(argument.Expression)) {
                return;
            }
        }

        // ⚠ Evaluation order changes: `n` used to run before the search arguments and now runs
        // after them. Demanding that both sides are name paths or literals removes the question
        // rather than reasoning about it.
        if (!IsSideEffectFree(offset.Expression)) {
            return;
        }

        // ⚠ Looked up, not assumed. `IndexOf(char, StringComparison)` exists and
        // `IndexOf(char, int, StringComparison)` does not, so a rule that appended `int` to any
        // overload it saw would write a call that does not bind.
        if (!HasStartIndexOverload(text, integer, searchMethod)) {
            return;
        }

        // ⚠ The result is an index into the *copy*. Only where the surrounding expression asks
        // "found or not" do the two spellings agree, because they differ by exactly the offset.
        if (!IsPresenceTest(search)) {
            return;
        }

        var deleted = TextSpan.FromBounds(substringAccess.OperatorToken.SpanStart, substring.Span.End);
        if (CallShape.ContainsComment(substring) || CallShape.ContainsComment(search.ArgumentList)) {
            return;
        }

        var insertion = search.ArgumentList.Arguments[0].Span.End;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                search.GetLocation(),
                FixEdits.Pack(
                    (deleted, string.Empty),
                    (new TextSpan(insertion, 0), ", " + offset.Expression)
                ),
                "`"
                + searchAccess.Name.Identifier.ValueText
                + "` takes the start index; the `Substring` "
                + "allocates a copy of the tail only to search inside it"
            )
        );
    }

    /// <summary>Whether <c>string</c> declares the same method with an <c>int</c> after its first parameter.</summary>
    static bool HasStartIndexOverload(INamedTypeSymbol text, INamedTypeSymbol integer, IMethodSymbol called) {
        foreach (var member in text.GetMembers(called.Name)) {
            if (member is not IMethodSymbol { IsStatic: false } candidate
                || candidate.Parameters.Length != called.Parameters.Length + 1
                || !SymbolEqualityComparer.Default.Equals(candidate.Parameters[1].Type, integer)
                || !SymbolEqualityComparer.Default.Equals(candidate.ReturnType, called.ReturnType)) {
                continue;
            }

            var matches = SymbolEqualityComparer.Default.Equals(
                candidate.Parameters[0].Type,
                called.Parameters[0].Type
            );

            for (var i = 1; matches && i < called.Parameters.Length; i++) {
                matches = SymbolEqualityComparer.Default.Equals(
                    candidate.Parameters[i + 1].Type,
                    called.Parameters[i].Type
                );
            }

            if (matches) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the invocation's value is only ever asked whether the search found anything.
    /// </summary>
    /// <remarks>
    ///     ⚠ Six comparisons and their mirrors, all of them against <c>0</c> or <c>-1</c>. Every one
    ///     of them is true of the rewritten call exactly when it is true of the original, because
    ///     "not found" is <c>-1</c> in both and every found position is non-negative in both. A
    ///     comparison against any other constant is a claim about <em>where</em>, and there the two
    ///     numbers differ by the offset.
    /// </remarks>
    static bool IsPresenceTest(ExpressionSyntax expression) {
        SyntaxNode node = expression;
        while (node.Parent is ParenthesizedExpressionSyntax parentheses) {
            node = parentheses;
        }

        if (node.Parent is not BinaryExpressionSyntax comparison) {
            return false;
        }

        var mirrored = ReferenceEquals(comparison.Right, node);
        var other = mirrored ? comparison.Left : comparison.Right;
        if (!IsConstant(other, out var bound)) {
            return false;
        }

        var kind = (SyntaxKind)comparison.RawKind;
        if (mirrored) {
            kind = kind switch {
                SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
                SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
                SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
                _ => kind
            };
        }

        return (kind, bound) switch {
            (SyntaxKind.GreaterThanOrEqualExpression, 0) => true,
            (SyntaxKind.LessThanExpression, 0) => true,
            (SyntaxKind.GreaterThanExpression, -1) => true,
            (SyntaxKind.LessThanOrEqualExpression, -1) => true,
            (SyntaxKind.EqualsExpression, -1) => true,
            (SyntaxKind.NotEqualsExpression, -1) => true,
            _ => false
        };
    }

    static bool IsConstant(ExpressionSyntax expression, out int value) {
        value = 0;
        switch (expression) {
            case LiteralExpressionSyntax { Token.Value: int literal }:
                value = literal;
                return true;

            case PrefixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.UnaryMinusExpression,
                Operand: LiteralExpressionSyntax { Token.Value: int negated }
            }:
                value = -negated;
                return true;

            default:
                return false;
        }
    }

    /// <summary>A literal, or a path of names; anything else may run something when it is read.</summary>
    static bool IsSideEffectFree(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax
        || expression is PrefixUnaryExpressionSyntax {
            RawKind: (int)SyntaxKind.UnaryMinusExpression,
            Operand: LiteralExpressionSyntax
        }
        || CallShape.IsPlainNamePath(expression);
}
