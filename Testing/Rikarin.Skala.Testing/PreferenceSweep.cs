using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rikarin.Skala.Testing;

/// <summary>
///     The preference fact, captured as data: at a fixed total width, which of two constructs on one
///     line gives when one break is needed — swept over the inner construct's own width, one column at
///     a time, at three constructs.
/// </summary>
/// <remarks>
///     ⚠ This sweep exists because of an end date, not because of a defect. SK-DIV-0050 § "The two facts
///     this family is made of" splits its family in two: the containment fact is a principle
///     ("a construct that spans lines makes its container span lines") and can be restated after
///     ReSharper is uninstalled, while the preference fact is only ever a measurement — SK-DIV-0005
///     records its stand-in as "a fitted constant, and the sweep says it is not a rule". A fitted
///     constant becomes unfalsifiable the moment the instrument goes away. So the instrument is asked
///     now, across a grid, and the answer is committed.
///     <para>
///         Three constructs, because the triage found the same curve at all three: the <c>=</c> against
///         the right-hand side's argument list (SK-DIV-0005), the lambda <c>=&gt;</c> against the body's
///         argument list (SK-DIV-0050), and the type parameter list against the parameter list
///         (SK-DIV-0024). Each is generated as one flat line of a chosen total width, with the width
///         split between an inert filler — the callee's or the method's own name, which holds no break
///         point — and the inner construct, so that moving one column from the filler into the list
///         changes nothing else about the line.
///     </para>
///     <para>
///         ⚠ <b>One column, not five.</b> The triage narrowed SK-DIV-0050's flip to a single column
///         (54) and SK-DIV-0024's to a single column (28 at a total of 124). A grid stepping by five
///         reports a smooth curve that does not exist. Both axes step by one.
///     </para>
///     <para>
///         ⚠ <b>Sampled, never bisected.</b> The recorded thresholds run 58 / 54 / 50 / 52 / 54 across
///         totals 125…171 — not monotone in the total — and this sweep finds rows that are not monotone
///         in the inner width either. A binary search over inner width would find one flip and report it
///         as the boundary. Every cell of every row is asked.
///     </para>
///     <para>
///         ⚠ <b>The filler's word lengths are a variable, not a constant.</b> Two divergence models
///         this repository recorded turned out to be artefacts of the probes that measured them, one
///         because its filler used five-letter words and a wrap budget of 113 was indistinguishable from
///         118. So the same grid is run under four filler profiles — including a deliberate five-letter
///         control — and a threshold that moves with the profile is a fact about the probe.
///     </para>
/// </remarks>
public static class PreferenceSweep {
    /// <summary>The margin the export sets, and the indent every probe sits at.</summary>
    const int Margin = 120;

    const int Indent = 4;

    /// <summary>The shortest filler name a probe will accept before the cell is dropped.</summary>
    /// <remarks>
    ///     ⚠ Four rather than one: a one-character callee makes the line's left half structurally
    ///     different from the rest of the row, and a row whose ends differ in kind cannot be read as one
    ///     curve.
    /// </remarks>
    const int MinimumFiller = 4;

    /// <summary>How many generated files one <c>cleanupcode</c> invocation is asked about.</summary>
    const int BatchSize = 60;

    /// <summary>What the oracle did with one generated line.</summary>
    public enum Outcome {
        /// <summary>Not generated — the total could not hold this inner width and a filler.</summary>
        Skipped,

        /// <summary>One line: it fitted. Never expected above the margin, and a bug in the probe if seen.</summary>
        Flat,

        /// <summary>The oracle took the outer break — the <c>=</c>, the <c>=&gt;</c>, or the <c>&lt;</c>.</summary>
        Outer,

        /// <summary>The oracle broke the inner construct instead — the argument or parameter list.</summary>
        Inner,

        /// <summary>The oracle broke somewhere this probe does not name. Recorded, never averaged away.</summary>
        Other
    }

    /// <summary>One generated line, and everything needed to classify what came back.</summary>
    /// <param name="Flat">The statement as one line, without its indentation.</param>
    /// <param name="OuterResume">
    ///     The column in <paramref name="Flat" /> a continuation starts at when the outer break is taken.
    /// </param>
    /// <param name="InnerResume">
    ///     The column in <paramref name="Flat" /> a continuation starts at when the inner list is broken.
    /// </param>
    sealed record Probe(
        string Construct,
        string Filler,
        int Total,
        int Inner,
        string Flat,
        int OuterResume,
        int InnerResume);

    /// <summary>A construct, as a way of turning (filler text, inner text) into a line and its landmarks.</summary>
    /// <param name="Wrap">
    ///     Builds the flat line from a filler name and the inner construct's own text, and reports where a
    ///     continuation line begins for each of the two competing breaks.
    /// </param>
    sealed record Construct(
        string Id,
        string Divergence,
        string Outer,
        string InnerName,
        string Template,
        Func<string, string, (string Flat, int OuterResume, int InnerResume)> Wrap,
        Func<IReadOnlyList<string>, string> File);

    /// <summary>A way of filling an inner construct to an exact width.</summary>
    /// <param name="TokenLengths">
    ///     The cycle of identifier lengths the list is built from. ⚠ <c>[5]</c> is the control: it is the
    ///     shape that produced a refuted finding, kept so that a threshold which only exists under it is
    ///     visible as a probe artefact rather than inherited as a fact.
    /// </param>
    sealed record Filler(string Id, int[] TokenLengths, string Description);

    /// <summary>One row of the grid: every inner width at one total, under one filler.</summary>
    public sealed record Row(
        string Construct,
        string Divergence,
        string Filler,
        int Total,
        int InnerFrom,
        string Codes);

    /// <summary>A place where the oracle's answer changes, to the column.</summary>
    /// <param name="Before">The last inner width on the low side of the flip.</param>
    /// <param name="After">The first inner width on the high side.</param>
    public sealed record Flip(
        string Construct,
        string Divergence,
        string Filler,
        int Total,
        int Before,
        int After,
        string From,
        string To,
        string BeforeText,
        string AfterText);

    /// <summary>The committed artefact.</summary>
    public sealed record Artefact(
        string Kind,
        int Version,
        string Oracle,
        string OracleVersion,
        string Profile,
        int MaxLineLength,
        int IndentSize,
        string Resolution,
        IReadOnlyList<string> Legend,
        IReadOnlyList<ConstructNote> Constructs,
        IReadOnlyList<FillerNote> Fillers,
        IReadOnlyList<Row> Grid,
        IReadOnlyList<Flip> Flips);

    public sealed record ConstructNote(
        string Id,
        string Divergence,
        string Template,
        string OuterBreak,
        string InnerConstruct);

    public sealed record FillerNote(string Id, IReadOnlyList<int> TokenLengths, string Description);

    static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    /// <summary>Keywords a generated identifier must never collide with, or the probe stops parsing.</summary>
    static readonly HashSet<string> Keywords = new(StringComparer.Ordinal) {
        "as", "base", "bool", "byte", "case", "char", "checked", "class", "const", "do", "else", "enum",
        "event", "false", "fixed", "for", "goto", "if", "in", "int", "is", "lock", "long", "new", "null",
        "out", "ref", "sbyte", "short", "sizeof", "stackalloc", "static", "this", "true", "try", "typeof",
        "uint", "ulong", "ushort", "using", "void", "while"
    };

    static List<Filler> Fillers() => [
        new(
            "uniform-5",
            [5],
            "Every identifier five characters — the control. This is the shape that made a wrap budget of "
            + "113 indistinguishable from 118 in a refuted finding, so it is swept deliberately: a "
            + "threshold that appears only here is a fact about the probe."
        ),
        new(
            "varied-short",
            [3, 8, 5, 11, 4, 7, 6, 9],
            "Identifier lengths cycling 3…11, so the comma positions land differently at every width."
        ),
        new(
            "varied-long",
            [12, 4, 6, 3, 9, 5, 14, 7],
            "A wider spread with two long identifiers per cycle, so a given width holds noticeably fewer "
            + "arguments than under varied-short."
        ),
        new(
            "single-literal",
            [],
            "One string literal argument, filling the list on its own — the shape of SK-DIV-0005's named "
            + "counter-example, `Convert.FromBase64String(\"…\")`, where the inner construct has no comma "
            + "to break at. Not applicable to a type parameter list."
        )
    ];

    static List<Construct> Constructs() => [
        new(
            "eq",
            "SK-DIV-0005",
            "=",
            "the right-hand side's argument list",
            "var value = <name>(<args>);",
            static (name, inner) => {
                var flat = "var value = " + name + inner + ";";
                return (flat, "var value = ".Length, "var value = ".Length + name.Length + 1);
            },
            static bodies => Body(bodies)
        ),
        new(
            "arrow",
            "SK-DIV-0050",
            "=>",
            "the lambda body's argument list",
            "Action value = () => <name>(<args>);",
            static (name, inner) => {
                const string head = "Action value = () => ";
                var flat = head + name + inner + ";";
                return (flat, head.Length, head.Length + name.Length + 1);
            },
            static bodies => Body(bodies)
        ),
        new(
            "type-parameters",
            "SK-DIV-0024",
            "<",
            "the type parameter list, against the parameter list",
            "public abstract void <name><T…>(int a, int b);",
            static (name, inner) => {
                const string head = "public abstract void ";
                const string tail = "(int a, int b);";
                var flat = head + name + inner + tail;
                return (flat, head.Length + name.Length + 1, head.Length + name.Length + inner.Length + 1);
            },
            static bodies => Declarations(bodies)
        )
    ];

    /// <summary>
    ///     Runs the grid and writes the two committed files.
    /// </summary>
    /// <param name="totals">The flat widths to sweep, in columns, indent included.</param>
    /// <param name="innerFrom">The narrowest inner construct to ask about, delimiters included.</param>
    /// <param name="innerTo">The widest.</param>
    public static Artefact Run(
        OracleRunner runner,
        string editorConfig,
        IReadOnlyList<int> totals,
        int innerFrom,
        int innerTo,
        TextWriter log
    ) {
        var constructs = Constructs();
        var fillers = Fillers();
        var scratch = Directory.CreateTempSubdirectory("skala-preference-");
        try {
            var files = new List<CorpusFile>();
            var plans = new Dictionary<string, (Construct Construct, List<Probe> Probes)>(StringComparer.Ordinal);

            foreach (var construct in constructs) {
                foreach (var filler in fillers) {
                    if (filler.TokenLengths.Length == 0 && construct.Id == "type-parameters") {
                        // ⚠ A type parameter list has no literals to hold. Skipped rather than faked.
                        continue;
                    }

                    foreach (var total in totals) {
                        var probes = new List<Probe>();
                        for (var inner = innerFrom; inner <= innerTo; inner++) {
                            var probe = Build(construct, filler, total, inner);
                            if (probe is not null) {
                                probes.Add(probe);
                            }
                        }

                        if (probes.Count == 0) {
                            continue;
                        }

                        var path = Path.Combine(
                            scratch.FullName,
                            construct.Id.Replace('-', '_')
                            + "__"
                            + filler.Id.Replace('-', '_')
                            + "__"
                            + total.ToString(CultureInfo.InvariantCulture)
                            + ".cs"
                        );

                        File.WriteAllText(path, construct.File([.. probes.Select(static probe => probe.Flat)]));
                        files.Add(new CorpusFile("preference", Path.GetFileName(path), path));
                        plans[path] = (construct, probes);
                    }
                }
            }

            log.WriteLine(
                "  "
                + files.Count.ToString(CultureInfo.InvariantCulture)
                + " sweep files, "
                + plans.Values.Sum(static plan => plan.Probes.Count).ToString(CultureInfo.InvariantCulture)
                + " probes"
            );

            // ⚠ Batched rather than one call. The full grid is ~600 files and several megabytes of
            // generated C#, and a single `cleanupcode` invocation over it is the shape of run that
            // dies at the far end with nothing to show. Each batch costs the tool's ~10 s startup
            // again, which is the price of being able to see the sweep advance.
            var results = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var offset = 0; offset < files.Count; offset += BatchSize) {
                var batch = files.GetRange(offset, Math.Min(BatchSize, files.Count - offset));
                foreach (var (path, formatted) in runner.Format(batch, editorConfig)) {
                    results[path] = formatted;
                }

                log.WriteLine(
                    "  "
                    + Math.Min(offset + BatchSize, files.Count).ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + files.Count.ToString(CultureInfo.InvariantCulture)
                    + " files"
                );
            }

            var grid = new List<Row>();
            var flips = new List<Flip>();

            foreach (var file in files) {
                var (construct, probes) = plans[file.Path];
                if (!results.TryGetValue(file.Path, out var formatted)) {
                    log.WriteLine("  ⚠ no result for " + file.RelativePath);
                    continue;
                }

                var groups = Split(formatted);
                var outcomes = new List<(Probe Probe, Outcome Outcome, string Text)>();
                for (var i = 0; i < probes.Count; i++) {
                    if (i >= groups.Count) {
                        outcomes.Add((probes[i], Outcome.Other, string.Empty));
                        continue;
                    }

                    outcomes.Add((probes[i], Classify(probes[i], groups[i]), string.Join(" ⏎ ", groups[i])));
                }

                grid.Add(Compress(construct, probes[0], outcomes, innerFrom, innerTo));
                flips.AddRange(Flips(construct, outcomes));
            }

            return new Artefact(
                "sk-div-preference-sweep",
                1,
                "jb cleanupcode",
                runner.Version,
                OracleRunner.Profile,
                Margin,
                Indent,
                Resolution(totals, innerFrom, innerTo),
                [
                    ".  not generated — the total cannot hold this inner width beside a filler",
                    "F  flat: one line",
                    "O  the oracle took the outer break",
                    "I  the oracle broke the inner construct instead",
                    "?  the oracle broke somewhere this probe does not name"
                ],
                [
                    .. constructs.Select(static construct => new ConstructNote(
                            construct.Id,
                            construct.Divergence,
                            construct.Template,
                            construct.Outer,
                            construct.InnerName
                        )
                    )
                ],
                [
                    .. fillers.Select(static filler =>
                        new FillerNote(filler.Id, filler.TokenLengths, filler.Description)
                    )
                ],
                [
                    .. grid
                        .OrderBy(static row => row.Construct, StringComparer.Ordinal)
                        .ThenBy(static row => row.Filler, StringComparer.Ordinal)
                        .ThenBy(static row => row.Total)
                ],
                [
                    .. flips
                        .OrderBy(static flip => flip.Construct, StringComparer.Ordinal)
                        .ThenBy(static flip => flip.Filler, StringComparer.Ordinal)
                        .ThenBy(static flip => flip.Total)
                        .ThenBy(static flip => flip.Before)
                ]
            );
        } finally {
            try {
                scratch.Delete(recursive: true);
            } catch (IOException) { }
        }
    }

    public static void Write(Artefact artefact, string jsonPath, string markdownPath) {
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(artefact, JsonOptions) + "\n");
        File.WriteAllText(markdownPath, Markdown(artefact, Path.GetFileName(jsonPath)));
    }

    static string Resolution(IReadOnlyList<int> totals, int innerFrom, int innerTo) =>
        "total "
        + totals[0].ToString(CultureInfo.InvariantCulture)
        + "…"
        + totals[^1].ToString(CultureInfo.InvariantCulture)
        + " step 1, inner "
        + innerFrom.ToString(CultureInfo.InvariantCulture)
        + "…"
        + innerTo.ToString(CultureInfo.InvariantCulture)
        + " step 1. Both axes step by one column because the flips the triage found are one column "
        + "wide, and every cell of every row is asked because the boundary is not monotone in either "
        + "axis, so a bisection would find one flip and miss the rest.";

    /// <summary>Builds one probe, or nothing when the total cannot hold this inner width.</summary>
    static Probe? Build(Construct construct, Filler filler, int total, int inner) {
        var text = filler.TokenLengths.Length == 0
            ? Literal(inner)
            : construct.Id == "type-parameters"
                ? TypeParameters(inner, filler.TokenLengths)
                : Arguments(inner, filler.TokenLengths);

        if (text is null) {
            return null;
        }

        // The filler name absorbs whatever the inner construct does not, so the flat line comes to
        // exactly `total`. Measured with a one-character name first, then padded by the shortfall.
        var (probeFlat, _, _) = construct.Wrap("x", text);
        var fillerLength = total - Indent - (probeFlat.Length - 1);
        if (fillerLength < MinimumFiller) {
            return null;
        }

        var (flat, outerResume, innerResume) = construct.Wrap(Name(fillerLength), text);
        return new Probe(construct.Id, filler.Id, total, inner, flat, outerResume, innerResume);
    }

    /// <summary>
    ///     A camel-cased filler name of an exact length.
    /// </summary>
    /// <remarks>
    ///     ⚠ Segmented rather than a run of one letter, for the same reason the argument lists vary their
    ///     word lengths: a forty-character run of <c>a</c> is a shape no real code has, and a threshold
    ///     that only holds for it is a fact about the generator.
    /// </remarks>
    static string Name(int length) {
        var builder = new StringBuilder("Do");
        var segment = 0;
        int[] segments = [5, 3, 7, 4, 6];
        while (builder.Length < length) {
            var take = Math.Min(segments[segment % segments.Length], length - builder.Length);
            builder.Append(Word(take, segment + 17), 0, take);
            segment++;
        }

        var name = builder.ToString(0, length);
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>A parenthesised argument list of exactly <paramref name="width" /> columns.</summary>
    static string? Arguments(int width, int[] lengths) {
        var inside = Tokens(width - 2, lengths, uppercase: false);
        return inside is null ? null : "(" + inside + ")";
    }

    /// <summary>An angle-bracketed type parameter list of exactly <paramref name="width" /> columns.</summary>
    static string? TypeParameters(int width, int[] lengths) {
        var inside = Tokens(width - 2, lengths, uppercase: true);
        return inside is null ? null : "<" + inside + ">";
    }

    /// <summary>A single string-literal argument filling the list on its own.</summary>
    static string? Literal(int width) =>
        // `("…")` — four columns of delimiter and quote before any content.
        width < 6 ? null : "(\"" + new string('Z', width - 4) + "\")";

    /// <summary>
    ///     Comma-separated identifiers coming to exactly <paramref name="width" /> columns, with lengths
    ///     drawn from <paramref name="lengths" /> in order and the last one trimmed to close the gap.
    /// </summary>
    static string? Tokens(int width, int[] lengths, bool uppercase) {
        if (width < 1) {
            return null;
        }

        var chosen = new List<int>();
        var remaining = width;
        var index = 0;
        while (true) {
            var separator = chosen.Count == 0 ? 0 : 2;
            var length = lengths[index % lengths.Length];
            index++;

            // Keep going only while a further identifier of at least one column could still close the
            // gap exactly; otherwise this one takes the remainder.
            if (remaining - separator - length >= 3) {
                chosen.Add(length);
                remaining -= separator + length;
                continue;
            }

            var last = remaining - separator;
            if (last < 1) {
                return null;
            }

            chosen.Add(last);
            break;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < chosen.Count; i++) {
            if (i > 0) {
                builder.Append(", ");
            }

            var word = Word(chosen[i], i);
            builder.Append(uppercase ? char.ToUpperInvariant(word[0]) + word[1..] : word);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     A pronounceable identifier of exactly <paramref name="length" /> characters, distinct per
    ///     <paramref name="seed" /> and never a keyword.
    /// </summary>
    static string Word(int length, int seed) {
        const string consonants = "bcdfghklmnprstvz";
        const string vowels = "aeiou";
        for (var attempt = 0; attempt < 8; attempt++) {
            var builder = new StringBuilder(length);
            var salt = seed * 7 + attempt * 3;
            for (var i = 0; i < length; i++) {
                builder.Append(
                    i % 2 == 0
                        ? consonants[(salt + i * 5) % consonants.Length]
                        : vowels[(salt + i * 3) % vowels.Length]
                );
            }

            var word = builder.ToString();
            if (!Keywords.Contains(word)) {
                return word;
            }
        }

        return "q" + new string('x', Math.Max(0, length - 1));
    }

    /// <summary>One statement per probe, separated by a blank line so the output splits back apart.</summary>
    static string Body(IReadOnlyList<string> statements) {
        var builder = new StringBuilder();
        builder.AppendLine("static class Sweep {");
        builder.AppendLine("    static void Body() {");
        foreach (var statement in statements) {
            builder.Append(new string(' ', Indent * 2)).AppendLine(statement);
            builder.AppendLine();
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    ///     One body-less member per probe.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>abstract</c> rather than the recorded entry's <c>public void … { }</c>, so that the
    ///     member ends at a <c>;</c> and no brace-placement key can add a line the splitter would have to
    ///     guess about. The declaration's head is wider by <c>abstract </c>, which the filler absorbs.
    /// </remarks>
    static string Declarations(IReadOnlyList<string> members) {
        var builder = new StringBuilder();
        builder.AppendLine("abstract class Sweep {");
        foreach (var member in members) {
            builder.Append(new string(' ', Indent)).AppendLine(member);
            builder.AppendLine();
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>Splits the formatted file back into one group of lines per probe.</summary>
    static List<List<string>> Split(string formatted) {
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

            if (trimmed is "static class Sweep {" or "abstract class Sweep {" or "static void Body() {" or "}") {
                continue;
            }

            current.Add(raw);
        }

        if (current.Count > 0) {
            groups.Add(current);
        }

        return groups;
    }

    /// <summary>
    ///     Names the first break the oracle made, by where the second line resumes in the flat text.
    /// </summary>
    /// <remarks>
    ///     ⚠ Positional rather than textual. "Does the first line end in <c>=</c>" would answer for one
    ///     construct and one value of one wrap-before key; "where does the continuation resume" answers
    ///     for all three constructs and cannot be fooled by an identifier that happens to end the same
    ///     way.
    /// </remarks>
    static Outcome Classify(Probe probe, List<string> group) {
        if (group.Count == 1) {
            return group[0].Trim() == probe.Flat ? Outcome.Flat : Outcome.Other;
        }

        var head = group[0].Trim();
        if (!probe.Flat.StartsWith(head, StringComparison.Ordinal)) {
            return Outcome.Other;
        }

        var resume = head.Length;
        if (resume < probe.Flat.Length && probe.Flat[resume] == ' ') {
            resume++;
        }

        return resume == probe.OuterResume
            ? Outcome.Outer
            : resume == probe.InnerResume
                ? Outcome.Inner
                : Outcome.Other;
    }

    static char Code(Outcome outcome) =>
        outcome switch {
            Outcome.Flat => 'F',
            Outcome.Outer => 'O',
            Outcome.Inner => 'I',
            Outcome.Other => '?',
            _ => '.'
        };

    static Row Compress(
        Construct construct,
        Probe first,
        List<(Probe Probe, Outcome Outcome, string Text)> outcomes,
        int innerFrom,
        int innerTo
    ) {
        var byInner = outcomes.ToDictionary(static entry => entry.Probe.Inner, static entry => entry.Outcome);
        var codes = new StringBuilder();
        for (var inner = innerFrom; inner <= innerTo; inner++) {
            codes.Append(byInner.TryGetValue(inner, out var outcome) ? Code(outcome) : '.');
        }

        return new Row(construct.Id, construct.Divergence, first.Filler, first.Total, innerFrom, codes.ToString());
    }

    /// <summary>Every place in a row where the answer changes, with the two lines either side of it.</summary>
    static IEnumerable<Flip> Flips(Construct construct, List<(Probe Probe, Outcome Outcome, string Text)> outcomes) {
        for (var i = 1; i < outcomes.Count; i++) {
            var (before, beforeOutcome, beforeText) = outcomes[i - 1];
            var (after, afterOutcome, afterText) = outcomes[i];
            if (beforeOutcome == afterOutcome || after.Inner != before.Inner + 1) {
                continue;
            }

            yield return new Flip(
                construct.Id,
                construct.Divergence,
                before.Filler,
                before.Total,
                before.Inner,
                after.Inner,
                beforeOutcome.ToString(),
                afterOutcome.ToString(),
                beforeText,
                afterText
            );
        }
    }

    static string Markdown(Artefact artefact, string jsonName) {
        var builder = new StringBuilder();
        builder.AppendLine("# The preference surface, measured");
        builder.AppendLine();
        builder.AppendLine(
            "**Two constructs on one line, one break needed, which one gives.** SK-DIV-0050 § \"The two"
        );
        builder.AppendLine(
            "facts this family is made of\" names this the *preference fact* and says the thing that makes"
        );
        builder.AppendLine(
            "it different from every other open divergence: it cannot be settled after the oracle is"
        );
        builder.AppendLine(
            "uninstalled. There is no principle to appeal to, only measurement, and the instrument goes"
        );
        builder.AppendLine("away. This file is the measurement, taken while it was still there.");
        builder.AppendLine();
        builder.Append("Oracle: `")
            .Append(artefact.Oracle)
            .Append(' ')
            .Append(artefact.OracleVersion)
            .Append("`, profile `")
            .Append(artefact.Profile)
            .Append("`, the repository `.editorconfig` unmodified — margin ")
            .Append(artefact.MaxLineLength.ToString(CultureInfo.InvariantCulture))
            .Append(", indent ")
            .Append(artefact.IndentSize.ToString(CultureInfo.InvariantCulture))
            .AppendLine(".");
        builder.AppendLine();
        builder.Append("The machine-readable grid is [`").Append(jsonName).AppendLine("`](" + jsonName + ").");
        builder.AppendLine();

        builder.AppendLine("## Resolution, and why");
        builder.AppendLine();
        builder.AppendLine(artefact.Resolution);
        builder.AppendLine();

        builder.AppendLine("## The three constructs");
        builder.AppendLine();
        builder.AppendLine("| id | divergence | template | outer break | inner construct |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (var construct in artefact.Constructs) {
            builder.Append("| `")
                .Append(construct.Id)
                .Append("` | ")
                .Append(construct.Divergence)
                .Append(" | `")
                .Append(construct.Template)
                .Append("` | `")
                .Append(construct.OuterBreak)
                .Append("` | ")
                .Append(construct.InnerConstruct)
                .AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine(
            "In every construct the total width is held fixed and moved one column at a time out of an"
        );
        builder.AppendLine(
            "inert filler — the callee's or the method's own name, which holds no break point — and into"
        );
        builder.AppendLine("the inner construct. Nothing else about the line changes.");
        builder.AppendLine();

        builder.AppendLine("## The filler profiles");
        builder.AppendLine();
        foreach (var filler in artefact.Fillers) {
            builder.Append("- **`")
                .Append(filler.Id)
                .Append("`** — identifier lengths ")
                .Append(
                    filler.TokenLengths.Count == 0
                        ? "n/a"
                        : "`[" + string.Join(", ", filler.TokenLengths) + "]`"
                )
                .Append(". ")
                .AppendLine(filler.Description);
        }

        builder.AppendLine();
        builder.AppendLine("## Legend");
        builder.AppendLine();
        foreach (var line in artefact.Legend) {
            builder.Append("- `").Append(line[0]).Append("` — ").AppendLine(line[3..]);
        }

        builder.AppendLine();
        builder.AppendLine("## Where the answer flips, to the column");
        builder.AppendLine();
        builder.AppendLine(
            "⚠ **The threshold is the finding.** A table of outputs without the boundary marked leaves"
        );
        builder.AppendLine("the next reader to re-derive it.");
        builder.AppendLine();

        foreach (var construct in artefact.Constructs) {
            var flips = artefact.Flips.Where(flip => flip.Construct == construct.Id).ToList();
            builder.Append("### `").Append(construct.Id).Append("` — ").AppendLine(construct.Divergence);
            builder.AppendLine();
            if (flips.Count == 0) {
                builder.AppendLine("No flip anywhere in the grid: the oracle's answer never changes with the");
                builder.AppendLine("inner construct's width at any total swept.");
                builder.AppendLine();
                continue;
            }

            builder.AppendLine("| filler | total | last | first | from | to |");
            builder.AppendLine("|---|---:|---:|---:|---|---|");
            foreach (var flip in flips) {
                builder.Append("| `")
                    .Append(flip.Filler)
                    .Append("` | ")
                    .Append(flip.Total.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(flip.Before.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(flip.After.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(flip.From)
                    .Append(" | ")
                    .Append(flip.To)
                    .AppendLine(" |");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## The grid");
        builder.AppendLine();
        builder.AppendLine(
            "One character per inner width, left to right, starting at the row's `inner from`. The raw"
        );
        builder.AppendLine("form of the same thing is in the JSON.");
        builder.AppendLine();

        foreach (var group in artefact.Grid.GroupBy(static row => (row.Construct, row.Filler))) {
            builder.Append("### `")
                .Append(group.Key.Construct)
                .Append("` × `")
                .Append(group.Key.Filler)
                .AppendLine("`");
            builder.AppendLine();
            builder.AppendLine("| total | inner from | outcome by inner width |");
            builder.AppendLine("|---:|---:|---|");
            foreach (var row in group) {
                builder.Append("| ")
                    .Append(row.Total.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(row.InnerFrom.ToString(CultureInfo.InvariantCulture))
                    .Append(" | `")
                    .Append(row.Codes)
                    .AppendLine("` |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}
