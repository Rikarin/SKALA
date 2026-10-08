using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     Which lines of a verbatim-emitted interpolated string belong to which multi-line raw literal, so the
///     writer can shift each literal's own lines to its own anchor. SK-DIV-0003, issue #447.
/// </summary>
/// <remarks>
///     ⚠ An interpolated string is emitted as one verbatim node (<see cref="NodeLayout.Verbatim" />), and a
///     plain raw literal is one token, which is why <c>skala_indent_raw_literal_string</c> reached the second
///     and never the first. Measured under <c>SkalaFormatOnly</c>: the oracle shifts a <c>$"""</c> literal's
///     content and closing delimiter to the column of its <c>"""</c> — not of its <c>$</c> — exactly as it
///     shifts a plain one; leaves every line that begins inside an interpolation hole where it was; and
///     shifts a <c>$"""</c> nested in a hole against <em>its own</em> quotes, after the line it opens on has
///     moved with the outer literal.
///     <para>
///         ⚠ Value-neutral for the plain literal's reason: C# strips the closing delimiter's whitespace
///         prefix from every content line of an interpolated raw literal too, so moving every line a
///         literal owns — and only those — by one amount leaves each of its text tokens' values
///         identical. A hole line is not the literal's and never moves; a nested literal's lines are its
///         own and move by its own amount. <see cref="TokenEquivalence" /> compares those values and is
///         the proof on every run.
///     </para>
/// </remarks>
static class RawLiteralPlan {
    /// <summary>
    ///     The plan for one verbatim node, or null when it holds no multi-line raw literal (or one whose
    ///     indentation carries a tab, which a column shift cannot reason about).
    /// </summary>
    /// <remarks>
    ///     The format is the writer's (<c>RawLiteralShift</c>): <c>col,line,closer,start;…|owner,owner,…</c> —
    ///     for each literal the width of its node line up to the quotes, the node line the quotes are on, the
    ///     width of its closing delimiter's indentation, and the width of its node line up to the literal's
    ///     first character (its <c>$</c>); then for each node line after the first, the literal that owns it,
    ///     <c>H</c> and the node line whose shift it takes when it begins in an interpolation hole, or
    ///     <c>-1</c>.
    /// </remarks>
    public static string? For(SyntaxNode node, string source) {
        var literals = new List<(TextSpanLike Span, int Quote, int Closer, InterpolatedStringExpressionSyntax? Holes)>();
        foreach (var candidate in node.DescendantNodesAndSelf(descendIntoTrivia: false)) {
            if (candidate is InterpolatedStringExpressionSyntax interpolated
                && interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken)) {
                var start = interpolated.StringStartToken;
                var dollars = start.Text.TakeWhile(static c => c == '$').Count();
                literals.Add(
                    (new(interpolated.SpanStart, interpolated.Span.End), start.SpanStart + dollars,
                        interpolated.StringEndToken.Span.End, interpolated)
                );
            }
        }

        foreach (var token in node.DescendantTokens(descendIntoTrivia: false)) {
            if (token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)) {
                literals.Add((new(token.SpanStart, token.Span.End), token.SpanStart, token.Span.End, null));
            }
        }

        if (literals.Count == 0) {
            return null;
        }

        literals.Sort(static (a, b) => a.Span.Start.CompareTo(b.Span.Start));

        var origin = node.SpanStart;
        var end = node.Span.End;
        var lineStarts = new List<int> { origin };
        for (var i = origin; i < end; i++) {
            if (source[i] == '\n') {
                lineStarts.Add(i + 1);
            }
        }

        var plan = new StringBuilder();
        for (var index = 0; index < literals.Count; index++) {
            var (_, quote, closerEnd, _) = literals[index];
            var quoteLine = LineOf(lineStarts, quote);

            // The closing delimiter's own line: its leading whitespace is what C# strips.
            var closerLineStart = source.LastIndexOf('\n', closerEnd - 1) + 1;
            var closerIndent = 0;
            while (source[closerLineStart + closerIndent] is ' ') {
                closerIndent++;
            }

            if (source[closerLineStart + closerIndent] == '\t') {
                return null;
            }

            if (index > 0) {
                plan.Append(';');
            }

            plan.Append(Width(source, lineStarts[quoteLine], quote).ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(quoteLine.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(closerIndent.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(Width(source, lineStarts[quoteLine], literals[index].Span.Start).ToString(CultureInfo.InvariantCulture));
        }

        plan.Append('|');
        for (var line = 1; line < lineStarts.Count; line++) {
            if (line > 1) {
                plan.Append(',');
            }

            var (owner, hole) = Owner(literals, lineStarts[line]);
            if (owner >= 0 || hole >= 0) {
                var at = lineStarts[line];
                while (at < end && source[at] is ' ') {
                    at++;
                }

                if (at < end && source[at] == '\t') {
                    return null;
                }
            }

            plan.Append(
                hole >= 0
                    ? "H" + LineOf(lineStarts, hole).ToString(CultureInfo.InvariantCulture)
                    : owner.ToString(CultureInfo.InvariantCulture)
            );
        }

        return plan.ToString();
    }

    /// <summary>
    ///     The innermost literal whose text holds a line starting at <paramref name="position" />, or -1 when
    ///     no literal does; and when the line begins inside one of that literal's interpolation holes, the
    ///     position of the hole's <c>{</c> instead.
    /// </summary>
    /// <remarks>
    ///     ⚠ A hole line moves by what the line its hole opens on moved, and that is the oracle's fixed
    ///     point rather than its first answer. Measured: on its first pass the oracle leaves the hole lines
    ///     of <c>Hello {</c> / <c>x</c> / <c>} there</c> where they were while <c>Hello {</c> moves four
    ///     left; given its own output back it moves them four left too, and a third pass changes nothing.
    ///     It re-indents a hole's lines from its opening line's indentation, read before the shift on the
    ///     first pass and after it on the second; Skala writes the second, in one pass (#372's rule).
    /// </remarks>
    static (int Owner, int Hole) Owner(
        List<(TextSpanLike Span, int Quote, int Closer, InterpolatedStringExpressionSyntax? Holes)> literals,
        int position
    ) {
        var owner = -1;
        for (var i = 0; i < literals.Count; i++) {
            var span = literals[i].Span;
            if (position > span.Start && position < span.End
                && (owner < 0 || span.End - span.Start < literals[owner].Span.End - literals[owner].Span.Start)) {
                owner = i;
            }
        }

        if (owner >= 0 && literals[owner].Holes is { } interpolated) {
            foreach (var content in interpolated.Contents) {
                if (content is InterpolationSyntax hole && position > hole.SpanStart && position < hole.Span.End) {
                    return (-1, hole.OpenBraceToken.SpanStart);
                }
            }
        }

        return (owner, -1);
    }

    static int LineOf(List<int> lineStarts, int position) {
        var line = 0;
        while (line + 1 < lineStarts.Count && lineStarts[line + 1] <= position) {
            line++;
        }

        return line;
    }

    static int Width(string source, int from, int to) => TextWidth.Measure(source[from..to]);

    readonly record struct TextSpanLike(int Start, int End);
}
