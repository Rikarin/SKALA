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
    /// <remarks>
    ///     ⚠ <b>Over <c>node.Span</c>, and the distinction is the whole defect this once had.</b> The
    ///     first version walked <c>node.DescendantTrivia()</c>, which covers <c>FullSpan</c> and
    ///     therefore the node's <em>leading</em> trivia — so a comment written <em>above</em> the code,
    ///     which no fix here deletes, declined the finding anyway. In a documented codebase that is
    ///     nearly every member, which makes a rule dead exactly where a linter is most wanted (#302).
    ///     <para>
    ///         ⚠ Every caller rewrites a sub-span of the node it passes — <c>SK4034</c> swaps two
    ///         member-name-to-end spans, the rest replace an argument list or a call — so
    ///         <c>node.Span</c> is the text at risk and <c>FullSpan</c> was never the question. A rule
    ///         whose fix deletes a whole <em>line</em> has the opposite need and must keep asking over
    ///         <c>FullSpan</c>, which is why this is not a change that can be made everywhere at once.
    ///     </para>
    /// </remarks>
    internal static bool ContainsComment(SyntaxNode node) =>
        Modernization.RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(node.SyntaxTree, node.Span);

    /// <summary>
    ///     Whether <paramref name="type" /> is the constructed form of the named generic type.
    /// </summary>
    internal static bool Is(ITypeSymbol? type, INamedTypeSymbol? definition) =>
        definition is not null
        && type is INamedTypeSymbol named
        && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, definition);
}
