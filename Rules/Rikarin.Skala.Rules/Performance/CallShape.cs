using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     The few questions every receiver-typed call-shape rule in this range has to ask.
/// </summary>
/// <remarks>
///     ⚠ <c>SK4030</c>–<c>SK4034</c> are one analysis with five method tables: take the receiver's
///     static type, look at the operator called on it, and decide whether the type itself already
///     offers the cheaper member. The three predicates below are what all five share, and they are here
///     rather than copied five times because each one is a place a rule gets quietly wrong — a fix that
///     deletes a comment, a receiver re-evaluated twice, a name matched instead of a symbol.
/// </remarks>
internal static class CallShape {
    /// <summary>
    ///     A path of names — <c>x</c>, <c>this.x</c>, <c>a.b.c</c> — and nothing else.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every fix in this range either repeats the receiver's text or moves it past another
    ///     expression, and both are only sound when reading it a second time runs nothing. An
    ///     invocation, an indexer or a <c>++</c> anywhere in the path makes the rewrite a change to how
    ///     many times something happens, which is not what any of these rules claim to be.
    /// </remarks>
    internal static bool IsPlainNamePath(ExpressionSyntax expression) {
        while (true) {
            switch (expression) {
                case IdentifierNameSyntax:
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                case PredefinedTypeSyntax:
                    return true;

                case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access:
                    expression = access.Expression;
                    continue;

                default:
                    return false;
            }
        }
    }

    /// <summary>A comment inside a span a fix replaces is content the fix would delete.</summary>
    internal static bool ContainsComment(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia()) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether <paramref name="type" /> is the constructed form of the named generic type.
    /// </summary>
    internal static bool Is(ITypeSymbol? type, INamedTypeSymbol? definition) =>
        definition is not null
        && type is INamedTypeSymbol named
        && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, definition);
}
