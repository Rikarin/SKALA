using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
///     The gaps Skala governs on purpose where the oracle keeps whatever the author wrote, and the
///     normalisation that takes them out of a comparison with the oracle.
/// </summary>
/// <remarks>
///     ⚠ One gap today: the one behind a collection expression's spread <c>..</c> (#513, SK-DIV-0310).
///     The oracle returns <c>[.. xs]</c> and <c>[..xs]</c> exactly as written at both values of
///     <c>space_within_spread_pattern</c>, so an oracle fixture records the corpus author's spelling,
///     not a rule; Skala writes the configured spelling. Compared raw, every corpus file written the
///     other way reads as a divergence — 39 lines over 19 files of <c>corpus/real/</c> when the
///     repository's configuration flipped to <c>false</c> — and a ratchet would be measuring which way
///     Vixen's authors happened to type, not whether Skala reproduces the oracle.
///     <para>
///         ⚠ So both sides lose the gap's horizontal space before they are compared. That is symmetric
///         and value-agnostic: it cannot make two texts agree that differ anywhere else, and it says
///         nothing about whether Skala honours the key, which <c>CastAndSpreadGapIssue450513Tests</c>
///         pins without the oracle. A gap holding a line break is left alone, so a kept or joined break
///         behind a <c>..</c> still counts. A slice pattern's <c>..</c> and a range's are not touched:
///         the first is the oracle's own rule and the second stays the author's on both sides.
///     </para>
///     <para>
///         ⚠ Parsed rather than matched: <c>[.. </c> is also how a range inside an indexer starts
///         (<c>a[.. n]</c>), and a regular expression cannot tell the two apart. Disabled <c>#if</c> text
///         is not parsed and is left as it is on both sides.
///     </para>
/// </remarks>
public static class SkalaGovernedGaps {
    static readonly CSharpParseOptions Options = new(LanguageVersion.Preview);

    public static string Normalise(string text) {
        if (!text.Contains("..", StringComparison.Ordinal)) {
            return text;
        }

        var root = CSharpSyntaxTree.ParseText(text, Options).GetRoot();
        var cuts = new List<(int Start, int End)>();
        foreach (var spread in root.DescendantNodes().OfType<SpreadElementSyntax>()) {
            var dots = spread.OperatorToken;
            var start = dots.Span.End;
            var end = start;
            while (end < text.Length && text[end] is ' ' or '\t') {
                end++;
            }

            // Only a run that reaches the operand on the same line: a comment or a line break behind
            // the `..` is not this gap.
            if (end > start && end == spread.Expression.SpanStart) {
                cuts.Add((start, end));
            }
        }

        if (cuts.Count == 0) {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var at = 0;
        foreach (var (start, end) in cuts.OrderBy(static cut => cut.Start)) {
            builder.Append(text, at, start - at);
            at = end;
        }

        builder.Append(text, at, text.Length - at);
        return builder.ToString();
    }
}
