using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     The guards the <c>SK1100</c>–<c>SK1103</c> statement rewrites share.
/// </summary>
/// <remarks>
///     ⚠ <see cref="RewriteGuards" /> answers the three questions an <em>expression</em> rewrite has
///     to answer. These five rules move whole <em>statements</em>, which adds two more: where does the
///     following statement live, and what happens to the text between the pieces that survive.
///     <para>
///         ⚠ The second question is the one that costs findings. Every rule here copies some spans
///         verbatim and deletes everything else in the region it rewrites, so a comment in a surviving
///         span is carried through and a comment anywhere else is <em>deleted</em>.
///         <see cref="DeletesAuthoredText" /> is that distinction, asked once per rewrite instead of
///         being re-derived per rule — the failure it prevents is a fix nobody can review, because the
///         sentence explaining why the code was written that way is gone from the diff.
///     </para>
/// </remarks>
internal static class StatementRewrites {
    /// <summary>
    ///     Whether the text a rewrite deletes — everything inside <paramref name="outer" /> that is not
    ///     one of the <paramref name="preserved" /> spans — holds a comment or a directive.
    /// </summary>
    /// <remarks>
    ///     ⚠ <paramref name="preserved" /> must be in source order and inside <paramref name="outer" />;
    ///     the gaps between them are what is checked. Written against the raw text rather than the
    ///     trivia list on purpose: <c>DescendantTrivia</c> includes a node's <em>leading</em> trivia
    ///     (#302), so a node-based check silently reaches outside the span it was asked about, and the
    ///     spans here are half-open regions of a statement rather than nodes at all.
    /// </remarks>
    public static bool DeletesAuthoredText(SyntaxTree tree, TextSpan outer, params TextSpan[] preserved) {
        var start = outer.Start;
        foreach (var span in preserved) {
            if (span.Start < start || span.End > outer.End) {
                return true;
            }

            if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(tree, TextSpan.FromBounds(start, span.Start))) {
                return true;
            }

            start = span.End;
        }

        return RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(tree, TextSpan.FromBounds(start, outer.End));
    }

    /// <summary>
    ///     The statement written immediately after <paramref name="statement" /> in the same statement
    ///     list, or <see langword="null" /> when there is none.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>switch</c> section holds a statement list and is not a <see cref="BlockSyntax" />, so
    ///     a rule that only looked at blocks would be silent inside every <c>switch</c> — which is
    ///     exactly where the `if`-then-`return` shape collects. An embedded statement (the body of an
    ///     unbraced <c>if</c>) has no following statement and returns <see langword="null" />, which is
    ///     the right answer: nothing follows it in its own scope.
    /// </remarks>
    public static StatementSyntax? Next(StatementSyntax statement) {
        var siblings = statement.Parent switch {
            BlockSyntax block => block.Statements,
            SwitchSectionSyntax section => section.Statements,
            _ => default
        };

        if (siblings.Count == 0) {
            return null;
        }

        var index = siblings.IndexOf(statement);
        return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
    }

    /// <summary>The whitespace the line holding <paramref name="position" /> starts with.</summary>
    /// <remarks>
    ///     ⚠ Copied rather than counted. One tab and one space occupy the same column and are different
    ///     indentation, so reconstructing an indent from a column count silently converts a file's
    ///     leading whitespace — and <c>skala fix</c> re-formats what it touches, so the only thing this
    ///     has to do is not be wrong before the formatter sees it.
    /// </remarks>
    public static string IndentAt(SourceText text, int position) {
        var line = text.Lines.GetLineFromPosition(position);
        var end = line.Start;
        while (end < line.End && (text[end] == ' ' || text[end] == '\t')) {
            end++;
        }

        return text.ToString(TextSpan.FromBounds(line.Start, end));
    }
}
