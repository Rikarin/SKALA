using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>A comment inside a span the fix replaces is content the fix would delete.</summary>
/// <remarks>
///     ⚠ This lived inside <c>CountPropertyAnalyzer.cs</c> until <c>SK1034</c> was retired, and ten
///     analyzers across three namespaces call it. A helper that nine rules depend on sitting in the
///     file of the tenth is why retiring one rule nearly took the build with it: deleting the rule
///     deletes the helper, and the failure surfaces as ten unrelated analyzers ceasing to compile.
///     It is its own file now so the next retirement is a delete and nothing else.
/// </remarks>
internal static class SyntaxSpanExtensions {
    public static bool SpanContainsComment(this SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia()) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)) {
                return true;
            }
        }

        return false;
    }
}
