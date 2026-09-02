using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
///     SK-DIV-0005, swept: where the oracle stops taking the <c>=</c> break, over shapes and depths.
/// </summary>
/// <remarks>
///     ⚠ Milestone 3 measured one shape at three depths and read a formula off three numbers. This runs
///     the same experiment over eleven right-hand-side shapes, five block depths and both values of
///     <c>wrap_before_eq</c>, one character at a time, and prints the threshold it finds for each cell
///     beside what <c>120 − (8 + column / indent)</c> predicts. A constant that survives a hundred cells
///     is a different kind of claim from one that fits three.
///     <para>
///         The classification is exact rather than eyeballed. Each statement is written
///         <c>var subject = &lt;rhs&gt;;</c> with the right-hand side padded to a known length, so the
///         oracle's answer is one of three things: one line, two lines whose second is exactly the
///         right-hand side, or anything else — flat, the <c>=</c> break alone, or the construct inside
///         wrapping.
///     </para>
/// </remarks>
public static class MarginSweep {
    /// <param name="Build">Produces a right-hand side of exactly <c>length</c> characters.</param>
    /// <param name="Minimum">The shortest right-hand side this shape can produce.</param>
    sealed record Shape(string Name, Func<int, string> Build, int Minimum);

    enum Layout {
        /// <summary>One line: it fitted.</summary>
        Flat,

        /// <summary>Two lines, broken at the <c>=</c>, the right-hand side whole.</summary>
        OuterBreak,

        /// <summary>Anything else: the construct inside the right-hand side wrapped.</summary>
        Inner
    }

    sealed record Cell(string Shape, int Depth, int Column, int Total, int? Longest, bool Monotone);

    const int Width = 120;
    const int Indent = 4;

    /// <summary>The two line widths the sweep runs at, both over the margin.</summary>
    /// <remarks>
    ///     ⚠ Two, because the first version of this sweep grew the right-hand side and kept the left one
    ///     fixed — which pins the continuation width at the moment the line first overflows and can only
    ///     ever probe one value of it. Growing the *name* instead, to a chosen total width, sweeps the
    ///     continuation independently of the overflow; running it at two totals is what shows the answer
    ///     does not depend on how far over the line was.
    /// </remarks>
    static readonly int[] Totals = [121, 137];

    static Shape Padded(string name, string prefix, string suffix) =>
        new(
            name,
            length => prefix + new string('a', Math.Max(0, length - prefix.Length - suffix.Length)) + suffix,
            prefix.Length + suffix.Length + 1
        );

    static List<Shape> Shapes() => [
        Padded("object-initializer", "new Employee { Name = \"", "\" }"),
        Padded("base64-literal", "Convert.FromBase64String(\"", "\")"),
        Padded("call-identifier", "TestFixtureBase.HexToBytes(", ")"),
        Padded("cast-call", "(JsonObjectContract)resolver.ResolveContract(typeof(", "))"),
        Padded("generic-call", "JsonConvert.DeserializeObject<Thing>(", ")"),
        Padded("collection-expression", "[1, 2, 3, ", "]"),
        Padded("array-initializer", "new[] { 1, 2, 3, ", " }"),
        Padded("binary-chain", "alpha + beta + ", ""),
        Padded("ternary", "flag ? alpha : ", ""),
        Padded("lambda-argument", "Assert.Throws<ParseException>(() => Read(", "))"),
        Padded("member-chain", "source.Where(Keep).Select(Project).OrderBy(", ").ToArray()")
    ];

    public static string Run(OracleRunner runner, string editorConfig, TextWriter log) {
        var builder = new StringBuilder();
        builder.AppendLine("# SK-DIV-0005 — where the oracle stops taking the `=` break");
        builder.AppendLine();
        builder.AppendLine("Each cell is the longest continuation line the oracle still writes rather than");
        builder.AppendLine("wrapping the right-hand side. The statement is `var <name> = <rhs>;` with the");
        builder.AppendLine("right-hand side padded to a known length and the *name* padded so that the flat");
        builder.AppendLine("line comes to exactly `total` — which sweeps the continuation width independently");
        builder.AppendLine("of how far over the margin the line was. `predicted` is milestone 3's");
        builder.AppendLine("`120 - (8 + column / 4)`.");
        builder.AppendLine();

        // ⚠ Written into the artefact rather than left to the reader. Ten of the shapes below are also
        // in the preference sweep, whose numbers answer a different question, and "read the floor off
        // this table" is the obvious economy and the wrong one — for reasons that file states.
        builder.AppendLine("⚠ **This file does not carry the wrapping *preference*, and its numbers cannot be read");
        builder.AppendLine("as one.** Each threshold here is where the `=` break's own continuation line stops being");
        builder.AppendLine("taken, measured by padding the *variable* name — so a wider right-hand side widens that");
        builder.AppendLine("continuation line at the same time, and the boundary confounds \"the inner break is now");
        builder.AppendLine(
            "enough\" with \"the outer break has stopped being enough\". Every shape below is swept again"
        );
        builder.AppendLine(
            "in [`sk-div-preference-sweep.md`](sk-div-preference-sweep.md) with the filler moved to the"
        );
        builder.AppendLine("other side of the `=`, and that file is where the floor `F` and the law's score per shape");
        builder.AppendLine("live. This one stays for what it does say: the threshold is depth-independent, it moves");
        builder.AppendLine("with the flat width, and it moves with the shape.");
        builder.AppendLine();

        foreach (var wrapBeforeEq in new[] { false, true }) {
            var cells = Measure(runner, editorConfig, wrapBeforeEq, log);
            builder.Append("## `wrap_before_eq = ")
                .Append(wrapBeforeEq ? "true`" : "false`")
                .AppendLine(wrapBeforeEq ? "" : " — the export's value");
            builder.AppendLine();
            builder.AppendLine("| shape | depth | column | total | longest `=`-break line | predicted | delta |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|");

            foreach (var cell in cells) {
                var predicted = Width - (8 + cell.Column / Indent);
                builder.Append("| ")
                    .Append(cell.Shape)
                    .Append(cell.Monotone ? "" : " ⚠")
                    .Append(" | ")
                    .Append(cell.Depth.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(cell.Column.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(cell.Total.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(cell.Longest?.ToString(CultureInfo.InvariantCulture) ?? "never")
                    .Append(" | ")
                    .Append(predicted.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(
                        cell.Longest is null
                            ? "—"
                            : (cell.Longest.Value - predicted).ToString("+#;-#;0", CultureInfo.InvariantCulture)
                    )
                    .AppendLine(" |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    static List<Cell> Measure(OracleRunner runner, string editorConfig, bool wrapBeforeEq, TextWriter log) {
        var scratch = Directory.CreateTempSubdirectory("skala-margin-");
        try {
            var files = new List<CorpusFile>();
            var plans = new Dictionary<string, (Shape Shape, int Depth, int Total, List<int> Lengths)>(
                StringComparer.Ordinal
            );

            foreach (var shape in Shapes()) {
                for (var depth = 2; depth <= 6; depth++) {
                    foreach (var total in Totals) {
                        var statement = depth * Indent;
                        var lengths = new List<int>();

                        // `var x = <rhs>;` — the name is padded so the flat line comes to `total`,
                        // which needs at least one character of name.
                        for (var rhs = shape.Minimum; rhs <= total - statement - 9; rhs++) {
                            lengths.Add(rhs);
                        }

                        if (lengths.Count == 0) {
                            continue;
                        }

                        var path = Path.Combine(
                            scratch.FullName,
                            shape.Name.Replace('-', '_')
                            + "_"
                            + depth.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + total.ToString(CultureInfo.InvariantCulture)
                            + ".cs"
                        );

                        File.WriteAllText(path, Source(shape, depth, total, lengths));
                        files.Add(new CorpusFile("margin", Path.GetFileName(path), path));
                        plans[path] = (shape, depth, total, lengths);
                    }
                }
            }

            log.WriteLine(
                "  wrap_before_eq="
                + (wrapBeforeEq ? "true" : "false")
                + ": "
                + files.Count.ToString(CultureInfo.InvariantCulture)
                + " sweep files"
            );

            var results = runner.Format(
                files,
                editorConfig,
                [new KeyValuePair<string, string>("resharper_csharp_wrap_before_eq", wrapBeforeEq ? "true" : "false")]
            );

            var cells = new List<Cell>();
            foreach (var file in files) {
                var (shape, depth, total, lengths) = plans[file.Path];
                var continuation = (depth + 1) * Indent;
                if (!results.TryGetValue(file.Path, out var formatted)) {
                    cells.Add(new Cell(shape.Name, depth, continuation, total, null, true));
                    continue;
                }

                var layouts = Classify(formatted, shape, depth, total, lengths, wrapBeforeEq);
                int? longest = null;
                var seenInner = false;
                var monotone = true;
                foreach (var (rhs, layout) in layouts) {
                    if (layout == Layout.OuterBreak) {
                        if (seenInner) {
                            monotone = false;
                        }

                        longest = Math.Max(longest ?? 0, continuation + rhs + 1);
                    } else if (layout == Layout.Inner) {
                        seenInner = true;
                    }
                }

                cells.Add(new Cell(shape.Name, depth, continuation, total, longest, monotone));
            }

            return cells;
        } finally {
            try {
                scratch.Delete(true);
            } catch (IOException) {
                // ⚠ Deliberate, and the `finally` is the reason. A scratch tree the oracle still has a
                // handle on is a leaked temp directory the OS reclaims; rethrowing here would replace
                // whatever exception is unwinding this `finally` with the cleanup's own, so the sweep
                // would report "could not delete" in place of the failure that actually stopped it.
            }
        }
    }

    /// <summary>The declaration's name, padded so the flat statement comes to <c>total</c> columns.</summary>
    static string Name(int depth, int total, int rhs) =>
        "s" + new string('x', Math.Max(0, total - depth * Indent - 9 - rhs));

    /// <summary>One statement per swept length, separated by a blank line so the output splits.</summary>
    static string Source(Shape shape, int depth, int total, List<int> lengths) {
        var builder = new StringBuilder();
        builder.AppendLine("static class Sweep {");
        builder.AppendLine("    static void Body() {");
        var opened = 0;
        for (var i = 2; i < depth; i++) {
            builder.Append(new string(' ', (i + 1) * Indent)).AppendLine("if (flag) {");
            opened++;
        }

        var inner = new string(' ', depth * Indent);
        foreach (var length in lengths) {
            builder.Append(inner)
                .Append("var ")
                .Append(Name(depth, total, length))
                .Append(" = ")
                .Append(shape.Build(length))
                .AppendLine(";");
            builder.AppendLine();
        }

        for (var i = opened + 1; i > 1; i--) {
            builder.Append(new string(' ', i * Indent)).AppendLine("}");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>Splits the formatted body back into one group of lines per statement.</summary>
    static List<(int Rhs, Layout Layout)> Classify(
        string formatted,
        Shape shape,
        int depth,
        int total,
        List<int> lengths,
        bool wrapBeforeEq
    ) {
        var continuation = (depth + 1) * Indent;
        var groups = new List<List<string>>();
        var current = new List<string>();
        foreach (var raw in formatted.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')) {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0) {
                if (current.Count > 0) {
                    groups.Add(current);
                    current = [];
                }

                continue;
            }

            if (trimmed.StartsWith("static ", StringComparison.Ordinal)
                || trimmed is "{" or "}"
                || trimmed.StartsWith("if (flag) {", StringComparison.Ordinal)) {
                continue;
            }

            current.Add(raw);
        }

        if (current.Count > 0) {
            groups.Add(current);
        }

        var results = new List<(int Rhs, Layout Layout)>();
        for (var i = 0; i < lengths.Count && i < groups.Count; i++) {
            var group = groups[i];

            // ⚠ With wrap_before_eq the `=` starts the continuation line rather than ending the
            // first one, so the shape of a two-line "outer break" answer is different text. It is
            // still the same decision, which is the point of sweeping both.
            var head = new string(' ', continuation) + (wrapBeforeEq ? "= " : string.Empty);
            var tail = head + shape.Build(lengths[i]) + ";";
            results.Add(
                (lengths[i],
                    group.Count == 1
                    ? Layout.Flat
                    : group.Count == 2 && string.Equals(group[1], tail, StringComparison.Ordinal)
                        ? Layout.OuterBreak
                        : Layout.Inner)
            );
        }

        return results;
    }
}
