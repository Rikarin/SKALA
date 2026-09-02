using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     The "is there anything between these two directives" question <c>SK7050</c> and <c>SK7052</c>
///     both ask.
/// </summary>
/// <remarks>
///     ⚠ Shared rather than duplicated. The two rules differed in exactly one way — which directive
///     kind is the one they are measuring and must therefore not count as content — and the rest of the
///     walk stood copied. A copy that drifts here changes what "empty" means in one rule and not the
///     other, and both rules' fixes delete a region on the strength of that answer.
/// </remarks>
internal static class RegionContent {
    /// <summary>
    ///     Every position in the tree that counts as content, in ascending order.
    /// </summary>
    /// <remarks>
    ///     ⚠ Zero-width tokens are skipped — the end-of-file token is one, and treating it as content
    ///     would make a trailing region look occupied by nothing.
    ///     <para>
    ///         Whitespace and end-of-line trivia never count, and neither do the directives named in
    ///         <paramref name="ignored" /> — a region's own <c>#region</c>/<c>#endregion</c>, or a
    ///         suppression's own <c>#pragma warning</c>. Every other piece of trivia does count: a
    ///         comment inside a region is a reason the region exists.
    ///     </para>
    /// </remarks>
    public static List<int> Positions(SyntaxNode root, SyntaxKind ignored, SyntaxKind? alsoIgnored = null) {
        var result = new List<int>();
        foreach (var token in root.DescendantTokens()) {
            if (token.Span.Length > 0) {
                result.Add(token.SpanStart);
            }
        }

        foreach (var trivia in root.DescendantTrivia()) {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)
                && !trivia.IsKind(ignored)
                && (alsoIgnored is not { } second || !trivia.IsKind(second))) {
                result.Add(trivia.SpanStart);
            }
        }

        result.Sort();
        return result;
    }
}
