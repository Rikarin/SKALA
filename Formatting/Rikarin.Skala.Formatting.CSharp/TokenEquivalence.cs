using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>Why two token streams differ, in words a bug report can use.</summary>
public sealed record EquivalenceFailure(int Index, string Before, string After);

/// <summary>
///     The safety net.
/// </summary>
/// <remarks>
///     docs/plan/04 § "The safety net" and ADR-005. Before any write, the significant token stream of
///     the output is compared with the input's. A failure is a Skala bug by definition: the file is
///     abandoned, nothing is written, <c>SK9099</c> is emitted and a reproduction is dropped under
///     <c>.skala/crash/</c>.
///     <para>
///         ⚠ There is no flag that turns this off — not for a hurry, not in CI, not in tests. A test that
///         needs to see bad output calls <see cref="CSharpDocumentBuilder" /> and the writer directly.
///     </para>
/// </remarks>
public static class TokenEquivalence {
    /// <summary>
    ///     ⚠ <paramref name="xmlDocReflow" /> widens the comparison for <c>///</c> comments <b>only</b>,
    ///     and only when the sub-formatter actually ran.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/04 § "The safety net" described this allowance as already present, and it was
    ///     not: <see cref="Normalise" /> trims each line and the one space after a marker, which no
    ///     re-wrap survives, because a re-wrap moves the line breaks. Adding it was therefore adding an
    ///     allowance rather than using one, and it is drawn as narrowly as a re-wrap permits: the
    ///     comparison becomes <see cref="XmlDocSignature" />, which is the same boundary the
    ///     sub-formatter refuses to cross. Everything a <c>///</c> comment is not, and every comment
    ///     when the flag is off, is compared exactly as before.
    ///     <para>
    ///         ⚠ It is not "ignore comments", and it is not even "the words in order" — that would have to
    ///         be widened again for <c>space_before_self_closing</c>, again for <c>spaces_inside_tags</c>,
    ///         and each widening is a class of damage the net stops seeing. The signature is <em>tighter</em>
    ///         than a word sequence where it counts: a <c>&lt;code&gt;</c> body is compared byte-for-byte.
    ///     </para>
    /// </remarks>
    /// <param name="xmlDocMarkerSpace">
    ///     ⚠ <c>space_after_triple_slash</c>, and it is passed rather than assumed. The signature reads a
    ///     verbatim body from the column after the marker's space, so at <c>false</c> — where the
    ///     sub-formatter neither adds that space nor takes one off — the net stays exactly as tight as it
    ///     was, and a <c>&lt;code&gt;</c> block that moved by a column is still a failure.
    /// </param>
    public static EquivalenceFailure? Compare(
        SourceText before,
        SourceText after,
        CSharpParseOptions parseOptions,
        bool xmlDocReflow = false,
        bool xmlDocMarkerSpace = true
    ) {
        var left = Significant(before, parseOptions, xmlDocReflow, xmlDocMarkerSpace);
        var right = Significant(after, parseOptions, xmlDocReflow, xmlDocMarkerSpace);

        var count = Math.Min(left.Count, right.Count);
        for (var i = 0; i < count; i++) {
            // ⚠ A doc comment's signature is compared by `XmlDocSignature.Matches`, which allows the one
            // break after an element's end the sub-formatter takes (#541, #542) and nothing else; a plain
            // comment's "C:" text never carries the separator characters that allowance reads.
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal)
                && !(xmlDocReflow
                    && left[i].StartsWith("C:", StringComparison.Ordinal)
                    && right[i].StartsWith("C:", StringComparison.Ordinal)
                    && XmlDocSignature.Matches(left[i], right[i]))) {
                return new(i, left[i], right[i]);
            }
        }

        if (left.Count != right.Count) {
            return new(
                count,
                count < left.Count ? left[count] : "(end of file)",
                count < right.Count ? right[count] : "(end of file)"
            );
        }

        return null;
    }

    /// <summary>
    ///     Every non-trivia token as <c>(RawKind, ValueText)</c>, plus the ordered comment texts, plus
    ///     every preprocessor directive in order, plus every disabled-text block verbatim.
    /// </summary>
    public static List<string> Significant(
        SourceText text,
        CSharpParseOptions parseOptions,
        bool xmlDocReflow = false,
        bool xmlDocMarkerSpace = true
    ) {
        var tree = CSharpSyntaxTree.ParseText(text, parseOptions);
        var items = new List<string>(1024);
        var builder = new StringBuilder();

        foreach (var token in tree.GetRoot().DescendantTokens(descendIntoTrivia: false)) {
            foreach (var trivia in token.LeadingTrivia) {
                AddTrivia(items, builder, trivia, xmlDocReflow, xmlDocMarkerSpace);
            }

            if (!token.IsKind(SyntaxKind.EndOfFileToken)) {
                builder.Clear();
                // ⚠ An interpolated raw literal's end token carries the closing delimiter's indentation
                // in its text, and `skala_indent_raw_literal_string` moves that indentation (#447). What
                // the indentation *means* — how much every content line is stripped by — is compared
                // through the text tokens' values, which Roslyn reports already stripped; the end
                // token's own whitespace says nothing a value does not.
                var value = token.IsKind(SyntaxKind.InterpolatedRawStringEndToken)
                    ? token.ValueText.TrimStart(' ', '\t', '\r', '\n')
                    : token.ValueText;
                builder.Append('T').Append(token.RawKind).Append(':').Append(value);
                items.Add(builder.ToString());
            }

            foreach (var trivia in token.TrailingTrivia) {
                AddTrivia(items, builder, trivia, xmlDocReflow, xmlDocMarkerSpace);
            }
        }

        return items;
    }

    static void AddTrivia(
        List<string> items,
        StringBuilder builder,
        SyntaxTrivia trivia,
        bool xmlDocReflow,
        bool xmlDocMarkerSpace
    ) {
        switch (trivia.Kind()) {
            case SyntaxKind.WhitespaceTrivia:
            case SyntaxKind.EndOfLineTrivia:
                return;

            case SyntaxKind.DisabledTextTrivia:
                // ⚠ Verbatim. The inactive branch is unstructured text and Skala never touches it,
                // so any difference at all is a defect.
                items.Add("D:" + trivia.ToFullString());
                return;

            // ⚠ A `/** … */` too since #489: the sub-formatter rebuilds a one-line one as a starred block, and
            // some multi-line ones since #568, and every one is held to the same signature.
            case SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia
                when xmlDocReflow && trivia.GetStructure() is DocumentationCommentTriviaSyntax structure:
                // ⚠ The allowance is the sub-formatter's own signature, not "comments are exempt"
                // and not "words in order". Words in order would have to be widened again for
                // `space_before_self_closing` and again for `spaces_inside_tags`, and each widening
                // is a class of damage the net stops seeing. The signature is narrower in the place
                // that matters most: a `<code>` body is compared byte-for-byte, which a
                // word-sequence comparison could never do.
                items.Add("C:" + XmlDocSignature.Of(structure, xmlDocMarkerSpace));
                return;

            case SyntaxKind.SingleLineCommentTrivia:
            case SyntaxKind.MultiLineCommentTrivia:
            case SyntaxKind.SingleLineDocumentationCommentTrivia:
            case SyntaxKind.MultiLineDocumentationCommentTrivia:
                // Normalised for the reindentation phase 1 does perform on `///` lines: the text is
                // compared, the leading whitespace of each line is not.
                items.Add("C:" + Normalise(builder, trivia.ToFullString()));
                return;

            default:
                if (trivia.IsDirective) {
                    items.Add("P:" + Normalise(builder, trivia.ToFullString()));
                    return;
                }

                if (trivia.Span.Length > 0) {
                    items.Add("S:" + trivia.ToFullString().Trim());
                }

                return;
        }
    }

    /// <summary>
    ///     Collapses each line's leading and trailing whitespace, and the one space after a comment
    ///     marker.
    /// </summary>
    /// <remarks>
    ///     ⚠ The marker space is normalised because inserting it is a rule Skala implements —
    ///     <c>space_after_triple_slash</c> and <c>space_before_trailing_comment_text</c> — and the
    ///     safety net must not treat a change it was asked to make as a lost token. Everything else
    ///     about the comment is compared exactly, so a swallowed word still fails.
    /// </remarks>
    static string Normalise(StringBuilder builder, string text) {
        builder.Clear();
        var first = true;
        foreach (var line in text.Split('\n')) {
            if (!first) {
                builder.Append('\n');
            }

            builder.Append(StripMarkerSpace(line.Trim()));
            first = false;
        }

        while (builder.Length > 0 && builder[^1] == '\n') {
            builder.Length--;
        }

        return builder.ToString();
    }

    static string StripMarkerSpace(string line) {
        foreach (var marker in new[] { "///", "//", "*" }) {
            if (line.StartsWith(marker, StringComparison.Ordinal)
                && line.Length > marker.Length
                && line[marker.Length] == ' ') {
                return marker + line[(marker.Length + 1)..];
            }
        }

        return line;
    }
}
