using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>One place Skala and the oracle disagree, classified by the construct it happened in.</summary>
public sealed record Divergence(string File, int Line, string Expected, string Actual, string Class);

/// <summary>The differential number, and the work queue behind it.</summary>
public sealed record FidelityReport(
    int Files,
    int IdenticalFiles,
    int Lines,
    int IdenticalLines,
    IReadOnlyList<Divergence> Divergences) {
    public double LineFidelity => Lines == 0 ? 1 : (double)IdenticalLines / Lines;

    public double FileFidelity => Files == 0 ? 1 : (double)IdenticalFiles / Files;

    /// <summary>
    /// ⚠ The output of a differential run is not pass/fail. It is a ranked report of divergence
    /// classes by line count, which is the work queue (docs/plan/12 § "Differential").
    /// </summary>
    public string Render(int topClasses = 20) {
        var builder = new StringBuilder();
        builder.Append("line fidelity: ")
            .Append((LineFidelity * 100).ToString("F2", CultureInfo.InvariantCulture))
            .Append("%  (")
            .Append(IdenticalLines.ToString(CultureInfo.InvariantCulture))
            .Append('/')
            .Append(Lines.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" lines)");
        builder.Append("file fidelity: ")
            .Append((FileFidelity * 100).ToString("F2", CultureInfo.InvariantCulture))
            .Append("%  (")
            .Append(IdenticalFiles.ToString(CultureInfo.InvariantCulture))
            .Append('/')
            .Append(Files.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" files)");
        builder.AppendLine();
        builder.AppendLine("divergence classes, by line count:");

        foreach (var group in Divergences
            .GroupBy(static d => d.Class, StringComparer.Ordinal)
            .OrderByDescending(static g => g.Count())
            .Take(topClasses)) {
            var files = group.Select(static d => d.File).Distinct(StringComparer.Ordinal).Count();
            builder.Append("  ")
                .Append(group.Count().ToString(CultureInfo.InvariantCulture).PadLeft(6))
                .Append("  lines across ")
                .Append(files.ToString(CultureInfo.InvariantCulture).PadLeft(4))
                .Append(" files  ")
                .AppendLine(group.Key);

            var sample = group.First();
            builder.Append("          ").AppendLine(
                sample.File + ":" + sample.Line.ToString(CultureInfo.InvariantCulture)
            );
            builder.Append("          oracle: ").AppendEscaped(Trim(sample.Expected)).AppendLine();
            builder.Append("          skala:  ").AppendEscaped(Trim(sample.Actual)).AppendLine();
        }

        return builder.ToString();
    }

    static string Trim(string line) => line.Length <= 110 ? line : line[..110] + "…";
}

/// <summary>
/// Compares Skala's output with the oracle's and groups the differences.
/// </summary>
/// <remarks>
/// ⚠ The comparison is a diff, not a positional walk. The oracle wraps lines and Skala (through
/// milestone 2) does not, so one wrapped call desynchronises a positional comparison for the rest
/// of the file and turns an honest 85 % into a meaningless 50 %. Line fidelity is
/// <em>matched lines ÷ total lines</em> over the longest common subsequence, which is what
/// docs/plan/12 § "Differential" means by "identical lines".
/// </remarks>
public static class Fidelity {
    public static FidelityReport Compare(IEnumerable<(string File, string Expected, string Actual)> results) {
        var files = 0;
        var identicalFiles = 0;
        var lines = 0;
        var identicalLines = 0;
        var divergences = new List<Divergence>();

        foreach (var (file, expected, actual) in results) {
            files++;
            var left = TextNormalisation.Lines(expected);
            var right = TextNormalisation.Lines(actual);
            lines += Math.Max(left.Length, right.Length);

            if (left.Length == right.Length && string.Equals(
                string.Join('\n', left),
                string.Join('\n', right),
                StringComparison.Ordinal
            )) {
                identicalFiles++;
                identicalLines += left.Length;
                continue;
            }

            var trace = LineDiff.Compute(left, right);
            identicalLines += trace.Count(static entry => entry.Kind == LineDiff.Kind.Same);
            Classify(file, trace, divergences);
        }

        return new FidelityReport(files, identicalFiles, lines, identicalLines, divergences);
    }

    /// <summary>
    /// Pairs each hunk's removed lines with its added lines so that a divergence is described as
    /// "the oracle wrote X, Skala wrote Y" rather than as two unrelated events.
    /// </summary>
    static void Classify(string file, IReadOnlyList<LineDiff.Entry> trace, List<Divergence> divergences) {
        var i = 0;
        var expectedLine = 0;
        while (i < trace.Count) {
            if (trace[i].Kind == LineDiff.Kind.Same) {
                expectedLine++;
                i++;
                continue;
            }

            var removed = new List<string>();
            var added = new List<string>();
            var start = expectedLine + 1;
            while (i < trace.Count && trace[i].Kind != LineDiff.Kind.Same) {
                if (trace[i].Kind == LineDiff.Kind.Removed) {
                    removed.Add(trace[i].Line);
                    expectedLine++;
                } else {
                    added.Add(trace[i].Line);
                }

                i++;
            }

            var count = Math.Max(removed.Count, added.Count);
            for (var k = 0; k < count; k++) {
                var left = k < removed.Count ? removed[k] : "(no line)";
                var right = k < added.Count ? added[k] : "(no line)";
                divergences.Add(
                    new Divergence(file, start + k, left, right, ClassOf(left, right, removed.Count, added.Count))
                );
            }
        }
    }

    /// <summary>
    /// The construct a difference occurred in, guessed from the two lines. Crude on purpose: the
    /// classes only have to be good enough to rank the work.
    /// </summary>
    static string ClassOf(string expected, string actual, int removedCount, int addedCount) {
        var left = expected.TrimStart();
        var right = actual.TrimStart();

        if (expected == "(no line)") {
            return right.Length == 0
                ? "blank line: Skala has one, the oracle does not"
                : removedCount < addedCount && addedCount > 1
                    ? "line break presence: Skala left a line the oracle joined (phase 2)"
                    : "extra line";
        }

        if (actual == "(no line)") {
            return left.Length == 0
                ? "blank line: the oracle has one, Skala does not"
                : "wrapping: the oracle broke a line Skala left long (phase 3)";
        }

        if (string.Equals(left, right, StringComparison.Ordinal)) {
            var difference = actual.Length - right.Length - (expected.Length - left.Length);
            return $"indentation ({difference:+#;-#;0} columns)";
        }

        if (left.Length == 0 || right.Length == 0) {
            return "blank line placement";
        }

        if (string.Equals(Squash(left), Squash(right), StringComparison.Ordinal)) {
            return "inter-token spacing";
        }

        if (left.StartsWith("//", StringComparison.Ordinal) && right.StartsWith("//", StringComparison.Ordinal)) {
            return "comment indentation";
        }

        if (left.StartsWith('#') || right.StartsWith('#')) {
            return "preprocessor directive placement";
        }

        if (right.StartsWith(left, StringComparison.Ordinal) || left.StartsWith(right, StringComparison.Ordinal)) {
            return "wrapping: one side continues where the other broke (phase 3)";
        }

        if (left is "{" or "}" || right is "{" or "}" || left.EndsWith('{') || right.EndsWith('{')) {
            return "brace placement";
        }

        return "other";
    }

    static string Squash(string line) {
        var builder = new StringBuilder(line.Length);
        foreach (var c in line) {
            if (c is not (' ' or '\t')) {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

/// <summary>A line-level longest-common-subsequence diff.</summary>
public static class LineDiff {
    public enum Kind {
        Same,
        Added,
        Removed
    }

    public readonly record struct Entry(Kind Kind, string Line);

    public static IReadOnlyList<Entry> Compute(string[] left, string[] right) {
        // Trim the common head and tail first: two formatter outputs agree on most of a file, and
        // the quadratic table is only paid for the middle.
        var head = 0;
        while (head < left.Length && head < right.Length && string.Equals(
            left[head],
            right[head],
            StringComparison.Ordinal
        )) {
            head++;
        }

        var tail = 0;
        while (tail < left.Length - head && tail < right.Length - head
            && string.Equals(left[^(tail + 1)], right[^(tail + 1)], StringComparison.Ordinal)) {
            tail++;
        }

        var result = new List<Entry>(left.Length + right.Length);
        for (var i = 0; i < head; i++) {
            result.Add(new Entry(Kind.Same, left[i]));
        }

        var innerLeft = left[head..(left.Length - tail)];
        var innerRight = right[head..(right.Length - tail)];
        Middle(innerLeft, innerRight, result);

        for (var i = right.Length - tail; i < right.Length; i++) {
            result.Add(new Entry(Kind.Same, right[i]));
        }

        return result;
    }

    static void Middle(string[] left, string[] right, List<Entry> result) {
        if (left.Length == 0 || right.Length == 0) {
            foreach (var line in left) {
                result.Add(new Entry(Kind.Removed, line));
            }

            foreach (var line in right) {
                result.Add(new Entry(Kind.Added, line));
            }

            return;
        }

        var table = new int[left.Length + 1, right.Length + 1];
        for (var i = left.Length - 1; i >= 0; i--) {
            for (var j = right.Length - 1; j >= 0; j--) {
                table[i, j] = string.Equals(left[i], right[j], StringComparison.Ordinal)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        var x = 0;
        var y = 0;
        while (x < left.Length && y < right.Length) {
            if (string.Equals(left[x], right[y], StringComparison.Ordinal)) {
                result.Add(new Entry(Kind.Same, left[x]));
                x++;
                y++;
            } else if (table[x + 1, y] >= table[x, y + 1]) {
                result.Add(new Entry(Kind.Removed, left[x]));
                x++;
            } else {
                result.Add(new Entry(Kind.Added, right[y]));
                y++;
            }
        }

        while (x < left.Length) {
            result.Add(new Entry(Kind.Removed, left[x++]));
        }

        while (y < right.Length) {
            result.Add(new Entry(Kind.Added, right[y++]));
        }
    }
}
