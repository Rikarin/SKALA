using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     A unified diff over the edits, for <c>--diff</c> and for the adoption step that reads what the
///     first run would do before it does it.
/// </summary>
public static class UnifiedDiff {
    const int Context = 3;

    public static string Render(string path, string before, string after) {
        var left = Split(before);
        var right = Split(after);
        var trace = LongestCommonSubsequence(left, right);

        var builder = new StringBuilder();
        var header = false;

        var index = 0;
        while (index < trace.Count) {
            if (trace[index].Kind == EditKind.Same) {
                index++;
                continue;
            }

            var start = Math.Max(0, index - Context);
            var end = index;
            while (end < trace.Count) {
                var run = 0;
                var probe = end;
                while (probe < trace.Count && trace[probe].Kind == EditKind.Same && run < Context * 2) {
                    probe++;
                    run++;
                }

                if (run >= Context * 2 || probe == trace.Count) {
                    end = Math.Min(trace.Count, end + Math.Min(run, Context));
                    break;
                }

                end = probe + 1;
            }

            if (!header) {
                builder.Append("--- a/").AppendLine(path);
                builder.Append("+++ b/").AppendLine(path);
                header = true;
            }

            var leftStart = trace.Take(start).Count(static e => e.Kind != EditKind.Added) + 1;
            var rightStart = trace.Take(start).Count(static e => e.Kind != EditKind.Removed) + 1;
            var leftCount = trace.Skip(start).Take(end - start).Count(static e => e.Kind != EditKind.Added);
            var rightCount = trace.Skip(start).Take(end - start).Count(static e => e.Kind != EditKind.Removed);

            builder.Append("@@ -")
                .Append(leftStart.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(leftCount.ToString(CultureInfo.InvariantCulture))
                .Append(" +")
                .Append(rightStart.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(rightCount.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" @@");

            for (var i = start; i < end; i++) {
                var (kind, line) = trace[i];
                builder.Append(
                    kind switch {
                        EditKind.Added => '+',
                        EditKind.Removed => '-',
                        _ => ' '
                    }
                )
                    .AppendLine(line);
            }

            index = end;
        }

        return builder.ToString();
    }

    static string[] Split(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    enum EditKind {
        Same,
        Added,
        Removed
    }

    readonly record struct Entry(EditKind Kind, string Line);

    /// <summary>
    ///     Classic dynamic-programming LCS. The inputs are one file's lines and the diff is only ever
    ///     rendered for a human, so the quadratic table is the right trade against a Myers walk.
    /// </summary>
    static List<Entry> LongestCommonSubsequence(string[] left, string[] right) {
        var table = new int[left.Length + 1, right.Length + 1];
        for (var i = left.Length - 1; i >= 0; i--) {
            for (var j = right.Length - 1; j >= 0; j--) {
                table[i, j] = string.Equals(left[i], right[j], StringComparison.Ordinal)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        var result = new List<Entry>(left.Length + right.Length);
        var x = 0;
        var y = 0;
        while (x < left.Length && y < right.Length) {
            if (string.Equals(left[x], right[y], StringComparison.Ordinal)) {
                result.Add(new Entry(EditKind.Same, left[x]));
                x++;
                y++;
            } else if (table[x + 1, y] >= table[x, y + 1]) {
                result.Add(new Entry(EditKind.Removed, left[x]));
                x++;
            } else {
                result.Add(new Entry(EditKind.Added, right[y]));
                y++;
            }
        }

        while (x < left.Length) {
            result.Add(new Entry(EditKind.Removed, left[x++]));
        }

        while (y < right.Length) {
            result.Add(new Entry(EditKind.Added, right[y++]));
        }

        return result;
    }
}
