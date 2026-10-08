using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Formatting;

/// <summary>
///     <see cref="VerbatimFlags.RealignRun" />: shifts the raw literals of one verbatim run, each to its own
///     anchor. SK-DIV-0003, issue #447.
/// </summary>
public static class RawLiteralShift {
    /// <param name="text">The run's text, as the source has it.</param>
    /// <param name="plan">The plan <c>RawLiteralPlan.For</c> wrote.</param>
    /// <param name="firstColumn">The output column the run's first character lands on.</param>
    /// <param name="firstLineIndent">The output indentation of the line the run starts on.</param>
    /// <param name="indentWidth">
    ///     Null under <c>align</c>, where a literal's target is the column of its quotes; the indent width under
    ///     <c>indent</c>, where it is the indentation of the line its quotes are on plus one level.
    /// </param>
    /// <param name="startsLine">Whether the run is the first thing on its output line.</param>
    public static string Apply(
        string text,
        string plan,
        int firstColumn,
        int firstLineIndent,
        int? indentWidth,
        bool startsLine
    ) {
        var parts = plan.Split('|');
        var literals = parts[0]
            .Split(';')
            .Select(static literal => literal.Split(',')
                    .Select(static n => int.Parse(n, CultureInfo.InvariantCulture))
                    .ToArray()
            )
            .ToArray();
        // Each line after the first: a literal it belongs to, or (`H<n>`) the earlier line whose shift a
        // hole line takes, or -1.
        var owners = parts[1].Length == 0 ? [] : parts[1].Split(',');

        var lines = text.Split('\n');
        if (owners.Length != lines.Length - 1) {
            return text;
        }

        // Literals are in source order and each one's quotes sit on a line that is either the first or
        // owned by a literal (or a hole) before it, so one pass in order resolves every delta.
        var deltas = new int?[literals.Length];

        int LineDelta(int line) {
            if (line == 0) {
                return 0;
            }

            var owner = owners[line - 1];
            if (owner[0] == 'H') {
                var opening = int.Parse(owner.AsSpan(1), CultureInfo.InvariantCulture);
                return opening < line ? LineDelta(opening) : 0;
            }

            var literal = int.Parse(owner, CultureInfo.InvariantCulture);
            return literal < 0 ? 0 : deltas[literal] ?? 0;
        }

        for (var i = 0; i < literals.Length; i++) {
            var (column, line, closer) = (literals[i][0], literals[i][1], literals[i][2]);
            int target;
            if (indentWidth is { } width) {
                // ⚠ A nested literal's line indent is the one its opening line *lands* on. The oracle's
                // first pass reads the one it was written at — `Outer {$"""` written at 24 and moved to 12
                // put the inner literal at 28 — and given that output back moves it to 16, where a third
                // pass leaves it. Skala writes the fixed point (#372's rule).
                // ⚠ And no level is added when the quotes start their line: a literal chopped onto a line
                // of its own lands under its own `$`, at 12 or 16 alike (#447, measured).
                var start = literals[i][3];
                var opensLine = line == 0 ? startsLine && start == 0 : LeadingWidth(lines[line]) == start;
                var lineIndent = line == 0 ? firstLineIndent : LeadingWidth(lines[line]) + LineDelta(line);
                target = opensLine ? lineIndent : lineIndent + width;
            } else {
                target = line == 0 ? firstColumn + column : column + LineDelta(line);
            }

            deltas[i] = target - closer;
        }

        var builder = new StringBuilder(text.Length);
        builder.Append(lines[0]);
        for (var line = 1; line < lines.Length; line++) {
            builder.Append('\n');
            var shift = LineDelta(line);
            var current = lines[line];
            var body = current.EndsWith('\r') ? current[..^1] : current;
            if (shift == 0 || body.AsSpan().TrimStart(' ').IsEmpty) {
                builder.Append(current);
                continue;
            }

            if (shift > 0) {
                builder.Append(' ', shift).Append(current);
                continue;
            }

            var removable = 0;
            while (removable < -shift && removable < body.Length && body[removable] == ' ') {
                removable++;
            }

            builder.Append(current, removable, current.Length - removable);
        }

        return builder.ToString();
    }

    static int LeadingWidth(string line) {
        var width = 0;
        while (width < line.Length && line[width] == ' ') {
            width++;
        }

        return width;
    }
}
