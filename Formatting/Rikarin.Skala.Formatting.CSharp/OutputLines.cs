using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>One node a blank-line rule asked about, and the answer the builder went on.</summary>
/// <param name="Span">The node's span, from its first token to its last.</param>
/// <param name="Guess">Whether the builder took it to occupy one output line within the margin.</param>
public readonly record struct LineQuestion(TextSpan Span, bool Guess);

/// <summary>
///     Reads off a layout whether each node a blank-line rule asked about occupies one output line within
///     the margin — the fact <c>blank_lines_around_single_line_*</c> and the multi-line statement keys
///     are about (issue #414).
/// </summary>
/// <remarks>
///     ⚠ The layout's text before the xmldoc and int-align post-passes, which is the text the anchors index.
///     Neither post-pass moves a line break in code.
/// </remarks>
public static class OutputLines {
    /// <summary>
    ///     The layout's answer for every question it can answer, and whether any of them differs from the
    ///     answer the document was built on.
    /// </summary>
    /// <remarks>
    ///     ⚠ A node whose first or last token has no anchor of its own — inside a <c>@formatter:off</c> span,
    ///     inside disabled text — is left out, and the builder keeps its guess for it. A guess that cannot
    ///     be checked is not a disagreement.
    /// </remarks>
    public static IReadOnlyDictionary<TextSpan, bool> Read(
        IReadOnlyList<LineQuestion> questions,
        Layout layout,
        int maxLineLength,
        out bool disagrees
    ) {
        disagrees = false;
        var answers = new Dictionary<TextSpan, bool>();
        if (questions.Count == 0) {
            return answers;
        }

        var starts = new Dictionary<int, int>();
        var ends = new Dictionary<int, int>();
        foreach (var anchor in layout.Anchors) {
            if (anchor.Source.Length == 0) {
                continue;
            }

            starts.TryAdd(anchor.Source.Start, anchor.OutputStart);

            // The last anchor ending at a position is the one the text ends with.
            ends[anchor.Source.End] = anchor.OutputEnd;
        }

        var text = layout.Text;
        foreach (var question in questions) {
            if (!answers.TryGetValue(question.Span, out var fact)) {
                if (!starts.TryGetValue(question.Span.Start, out var from)
                    || !ends.TryGetValue(question.Span.End, out var to)
                    || to < from) {
                    continue;
                }

                fact = OneLineWithinMargin(text, from, to, maxLineLength);
                answers[question.Span] = fact;
            }

            disagrees |= fact != question.Guess;
        }

        return answers;
    }

    /// <summary>
    ///     Whether <c>text[from..to]</c> holds no line break and the output line it is on fits the margin.
    /// </summary>
    /// <remarks>
    ///     ⚠ The margin half is not decoration. A member the fitter could not break — the return type and
    ///     name of a 140-column declaration, which Skala keeps together where the oracle breaks between
    ///     them (SK-DIV-0024) — is one line in Skala's output and two in the oracle's, and the guess has
    ///     always called it multi-line by its width; reading only the line count would move its blank
    ///     lines away from the oracle's. The whole line is measured, indentation and trailing comment
    ///     included, because that is the line the fitter measured.
    /// </remarks>
    static bool OneLineWithinMargin(string text, int from, int to, int maxLineLength) {
        if (text.AsSpan(from, to - from).IndexOfAny('\n', '\r') >= 0) {
            return false;
        }

        var lineStart = from == 0 ? 0 : text.AsSpan(0, from).LastIndexOfAny('\n', '\r') + 1;
        var lineEnd = text.AsSpan(to).IndexOfAny('\n', '\r');
        lineEnd = lineEnd < 0 ? text.Length : to + lineEnd;
        return TextWidth.Measure(text[lineStart..lineEnd]) <= maxLineLength;
    }
}
