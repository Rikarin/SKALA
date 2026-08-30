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

        /// <summary>
        ///     The oracle took a third break point the construct names, and neither of the two the
        ///     divergence is about.
        /// </summary>
        /// <remarks>
        ///     ⚠ This exists because the first run of this sweep found it. 17 % of the type parameter
        ///     grid came back as "somewhere this probe does not name", and what the oracle was actually
        ///     doing was breaking between the return type and the method name — declining *both* lists.
        ///     A binary probe would have recorded that as noise; it is instead the most important thing
        ///     the type parameter construct has to say, because "which of these two gives" is the wrong
        ///     question wherever a third answer wins.
        /// </remarks>
        Third,

        /// <summary>The oracle broke somewhere this probe does not name. Recorded, never averaged away.</summary>
        Other
    }

    /// <summary>
    ///     The columns of the flat line at which a continuation may resume for one break point.
    /// </summary>
    /// <remarks>
    ///     ⚠ A span rather than a column, because the first run of this sweep classified the type
    ///     parameter list by the column just after its <c>&lt;</c> and could not name a single cell where
    ///     the oracle wrapped it. SK-DIV-0024 already says why: at
    ///     <c>wrap_before_type_parameter_langle = false</c> the oracle wraps that list <em>as a fill</em>,
    ///     so the break lands at whichever comma runs out of room, not at the opening bracket. A break
    ///     point that can be taken at more than one column has to be asked about as more than one column.
    /// </remarks>
    readonly record struct Span(int From, int To) {
        public static Span Point(int column) => new(column, column + 1);

        public bool Contains(int column) => column >= From && column < To;
    }

    /// <summary>A construct's flat line, with every column of it the classifier needs.</summary>
    /// <param name="Head">
    ///     How wide the line is that remains when the inner construct is broken, indent excluded.
    /// </param>
    /// <remarks>
    ///     ⚠ <paramref name="Head" /> is stated rather than derived from <paramref name="Inner" />, and
    ///     the difference is a column. A break after <c>(</c> leaves the head ending at the bracket; a
    ///     break after <c>{</c> leaves it ending at the brace, but the flat text has a space after that
    ///     brace which the break eats. Inferring one from the other is right for argument lists and
    ///     wrong for initialisers by exactly the amount that decides whether a boundary sits at the
    ///     margin.
    /// </remarks>
    sealed record Layout(string Flat, Span Outer, Span Inner, Span? Third, int Head);

    /// <summary>One generated line, and everything needed to classify what came back.</summary>
    /// <param name="Flat">The statement as one line, without its indentation.</param>
    /// <param name="Outer">Where a continuation may resume when the outer break is taken.</param>
    /// <param name="Inner">Where a continuation may resume when the inner construct is broken.</param>
    /// <param name="Third">The construct's third named break, when it has one.</param>
    sealed record Probe(
        string Construct,
        string Filler,
        int Total,
        int Inner,
        string Flat,
        Span Outer,
        Span InnerSpan,
        Span? Third,
        int Head);

    /// <summary>A construct, as a way of turning (filler text, inner text) into a line and its landmarks.</summary>
    /// <param name="Wrap">
    ///     Builds the flat line from a filler name and the inner construct's own text, and reports where a
    ///     continuation line begins for each competing break: the outer one, the inner one, and a third
    ///     the construct names but the divergence is not about — <c>-1</c> when there is none.
    /// </param>
    /// <param name="ThirdName">What the third break is, in words, or nothing when the construct has none.</param>
    /// <param name="Depth">
    ///     How many indents the generated line sits at inside its wrapper.
    /// </param>
    /// <remarks>
    ///     ⚠ <paramref name="Depth" /> exists because getting it wrong is silent and fatal. A statement
    ///     inside a method body sits two indents in, not one, and a sweep that treats its <c>total</c> as
    ///     the flat line's width is then reporting every column four short — which is exactly enough to
    ///     hide a boundary that sits at the margin, and to make a rule look like a fitted constant.
    /// </remarks>
    sealed record Construct(
        string Id,
        string Divergence,
        string Outer,
        string InnerName,
        string Template,
        Func<string, string, Layout> Wrap,
        Func<IReadOnlyList<string>, string> File,
        int Depth,
        string? ThirdName = null);

    /// <summary>A way of filling an inner construct to an exact width.</summary>
    /// <param name="TokenLengths">
    ///     The cycle of identifier lengths the list is built from. ⚠ <c>[5]</c> is the control: it is the
    ///     shape that produced a refuted finding, kept so that a threshold which only exists under it is
    ///     visible as a probe artefact rather than inherited as a fact.
    /// </param>
    sealed record Filler(string Id, int[] TokenLengths, string Description);

    /// <summary>One row of the grid: every inner width at one total, under one filler.</summary>
    /// <param name="Sufficient">
    ///     The narrowest inner width in this row at which breaking the inner construct brings the head
    ///     line within the margin on its own — the prediction of the only closed form worth testing.
    /// </param>
    /// <remarks>
    ///     ⚠ <paramref name="Sufficient" /> is a *hypothesis*, recorded beside the measurement so the two
    ///     can be compared without re-running anything: "the oracle breaks the inner construct exactly
    ///     when doing so is enough by itself, and reaches further out when it is not." Where it matches
    ///     the measured threshold the divergence has a rule and needs no oracle; where it does not, what
    ///     is left is preference, and preference is the thing that cannot be re-derived later.
    /// </remarks>
    public sealed record Row(
        string Construct,
        string Divergence,
        string Filler,
        int Total,
        int InnerFrom,
        string Codes,
        int? Sufficient = null);

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
        IReadOnlyList<Flip> Flips,
        IReadOnlyList<Unnamed> Unnamed);

    /// <summary>
    ///     A cell the probe could not name, kept verbatim.
    /// </summary>
    /// <remarks>
    ///     ⚠ An outcome a probe cannot name is the most dangerous kind of cell in a grid, because it
    ///     looks like noise and is usually a break point the experiment did not know about. The first run
    ///     of this sweep binned 17 % of one construct that way, and what was in the bin was the oracle
    ///     declining both of the constructs the divergence is about. So the unnamed cells are carried
    ///     into the artefact rather than counted in it: one exemplar per distinct rendering shape.
    /// </remarks>
    public sealed record Unnamed(
        string Construct,
        string Filler,
        int Total,
        int Inner,
        int Count,
        string Text);

    public sealed record ConstructNote(
        string Id,
        string Divergence,
        string Template,
        string OuterBreak,
        string InnerConstruct,
        string? ThirdBreak = null);

    public sealed record FillerNote(string Id, IReadOnlyList<int> TokenLengths, string Description);

    static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    /// <summary>Keywords a generated identifier must never collide with, or the probe stops parsing.</summary>
    static readonly HashSet<string> Keywords = new(StringComparer.Ordinal) {
        "as",
        "base",
        "bool",
        "byte",
        "case",
        "char",
        "checked",
        "class",
        "const",
        "do",
        "else",
        "enum",
        "event",
        "false",
        "fixed",
        "for",
        "goto",
        "if",
        "in",
        "int",
        "is",
        "lock",
        "long",
        "new",
        "null",
        "out",
        "ref",
        "sbyte",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "this",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "ushort",
        "using",
        "void",
        "while"
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
                const string head = "var value = ";
                var flat = head + name + inner + ";";
                var open = head.Length + name.Length;
                return new Layout(
                    flat,
                    Span.Point(head.Length),
                    new Span(open + 1, open + inner.Length),
                    null,
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2
        ),
        new(
            "eq-array",
            "SK-DIV-0005",
            "=",
            "the right-hand side's array initialiser",
            "var value = new <name>[] { <elements> };",
            // ⚠ The filler is the element type, so it sits on the right of the `=` exactly as the
            // callee's name does in `eq`. Padding the *variable* name instead would move the filler to
            // the other side of the break under test and change how much the `=` buys — two things at
            // once, and then a difference between the two constructs would say nothing about shape.
            static (name, inner) => {
                const string head = "var value = new ";
                var flat = head + name + "[] " + inner + ";";
                var open = head.Length + name.Length + 3;
                // ⚠ `{ a` — the head ends at the brace and the break eats the space after it, so the
                // head is one column narrower than where the continuation resumes.
                return new Layout(
                    flat,
                    Span.Point("var value = ".Length),
                    new Span(open + 2, open + inner.Length - 1),
                    null,
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2
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
                var open = head.Length + name.Length;
                // ⚠ The third landmark is the `=`. SK-DIV-0050 records Skala taking it where the oracle
                // takes the arrow, so the sweep names it rather than binning it: a grid in which the
                // oracle never once prefers it is evidence, and an unnamed outcome is not.
                return new Layout(
                    flat,
                    Span.Point(head.Length),
                    new Span(open + 1, open + inner.Length),
                    Span.Point("Action value = ".Length),
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2,
            "the `=` above the lambda"
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
                var langle = head.Length + name.Length;
                var lparen = langle + inner.Length;
                // ⚠ The third landmark is the gap between the return type and the method name, and it
                // is the one the first run of this sweep discovered by leaving 17 % of the grid
                // unnamed. The oracle reaches for it constantly, and where it does, "which of the two
                // lists gives" has no answer because neither of them does.
                return new Layout(
                    flat,
                    new Span(langle + 1, langle + inner.Length),
                    new Span(lparen + 1, lparen + tail.Length - 1),
                    Span.Point(head.Length),
                    lparen + 1
                );
            },
            static bodies => Declarations(bodies),
            1,
            "the gap after the return type, before the method name"
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
            var plans = Generate(constructs, fillers, totals, innerFrom, innerTo, scratch.FullName);
            var files = plans.Keys
                .Order(StringComparer.Ordinal)
                .Select(static path => new CorpusFile("preference", Path.GetFileName(path), path))
                .ToList();

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
            var unnamed = new Dictionary<string, Unnamed>(StringComparer.Ordinal);

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

                foreach (var (probe, outcome, text) in outcomes) {
                    if (outcome != Outcome.Other) {
                        continue;
                    }

                    // Keyed by where the continuation resumes, so one exemplar is kept per *shape* of
                    // answer rather than per cell — and the count says how much of the grid it covers.
                    var key = construct.Id + ":" + Shape(probe, text);
                    unnamed[key] = unnamed.TryGetValue(key, out var seen)
                        ? seen with { Count = seen.Count + 1 }
                        : new Unnamed(construct.Id, probe.Filler, probe.Total, probe.Inner, 1, text);
                }
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
                    "T  the oracle took the construct's third break and declined both of the two",
                    "?  the oracle broke somewhere this probe does not name"
                ],
                [
                    .. constructs.Select(static construct => new ConstructNote(
                            construct.Id,
                            construct.Divergence,
                            construct.Template,
                            construct.Outer,
                            construct.InnerName,
                            construct.ThirdName
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
                ],
                [
                    .. unnamed.Values
                        .OrderByDescending(static entry => entry.Count)
                        .ThenBy(static entry => entry.Construct, StringComparer.Ordinal)
                ]
            );
        } finally {
            try {
                scratch.Delete(recursive: true);
            } catch (IOException) { }
        }
    }

    /// <summary>
    ///     Writes one file per (construct, filler, total) into the scratch directory and returns what
    ///     each of them holds.
    /// </summary>
    /// <remarks>
    ///     ⚠ One file per row rather than one per cell. <c>cleanupcode</c> costs seconds of startup and
    ///     pennies of analysis, so fifty thousand single-statement files would be a day's run and fifty
    ///     thousand statements in one file is a solution the tool declines to open.
    /// </remarks>
    static Dictionary<string, (Construct Construct, List<Probe> Probes)> Generate(
        List<Construct> constructs,
        List<Filler> fillers,
        IReadOnlyList<int> totals,
        int innerFrom,
        int innerTo,
        string scratch
    ) {
        var plans = new Dictionary<string, (Construct Construct, List<Probe> Probes)>(StringComparer.Ordinal);
        foreach (var construct in constructs) {
            foreach (var filler in fillers) {
                // ⚠ A type parameter list has no literals to hold. Skipped rather than faked.
                if (filler.TokenLengths.Length == 0 && construct.Id == "type-parameters") {
                    continue;
                }

                foreach (var total in totals) {
                    var probes = Enumerable.Range(innerFrom, innerTo - innerFrom + 1)
                        .Select(inner => Build(construct, filler, total, inner))
                        .OfType<Probe>()
                        .ToList();

                    if (probes.Count == 0) {
                        continue;
                    }

                    var path = Path.Combine(
                        scratch,
                        construct.Id.Replace('-', '_')
                        + "__"
                        + filler.Id.Replace('-', '_')
                        + "__"
                        + total.ToString(CultureInfo.InvariantCulture)
                        + ".cs"
                    );

                    File.WriteAllText(path, construct.File([.. probes.Select(static probe => probe.Flat)]));
                    plans[path] = (construct, probes);
                }
            }
        }

        return plans;
    }

    public static void Write(Artefact artefact, string jsonPath, string markdownPath) {
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(artefact, JsonOptions) + "\n");
        File.WriteAllText(markdownPath, Markdown(artefact, Path.GetFileName(jsonPath)));
    }

    /// <summary>
    ///     Reads a committed grid back and rewrites the prose beside it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The measurement costs minutes of an installed ReSharper and the reading of it costs nothing,
    ///     so they are separable on purpose: every sentence in the markdown is computed from the JSON, and
    ///     a reader who wants to ask the grid a question the prose does not answer can change the question
    ///     without re-running the oracle — which, after the oracle is uninstalled, is the only way left.
    /// </remarks>
    public static Artefact Read(string jsonPath) =>
        JsonSerializer.Deserialize<Artefact>(File.ReadAllText(jsonPath), JsonOptions)
        ?? throw new InvalidOperationException(jsonPath + " did not deserialise.");

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
        var text = (construct.Id, filler.TokenLengths.Length) switch {
            ("eq-array", 0) => BracedLiteral(inner),
            ("eq-array", _) => Braced(inner, filler.TokenLengths),
            ("type-parameters", _) => TypeParameters(inner, filler.TokenLengths),
            (_, 0) => Literal(inner),
            _ => Arguments(inner, filler.TokenLengths)
        };

        if (text is null) {
            return null;
        }

        // The filler name absorbs whatever the inner construct does not, so the flat line comes to
        // exactly `total`. Measured with a one-character name first, then padded by the shortfall.
        var probe = construct.Wrap("x", text);
        var fillerLength = total - (construct.Depth * Indent) - (probe.Flat.Length - 1);
        if (fillerLength < MinimumFiller) {
            return null;
        }

        var layout = construct.Wrap(Name(fillerLength), text);
        return new Probe(
            construct.Id,
            filler.Id,
            total,
            inner,
            layout.Flat,
            layout.Outer,
            layout.Inner,
            layout.Third,
            layout.Head
        );
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

    /// <summary>A braced initialiser list of exactly <paramref name="width" /> columns.</summary>
    /// <remarks>⚠ <c>{ a, b }</c> — four columns of delimiter and padding, not two.</remarks>
    static string? Braced(int width, int[] lengths) {
        var inside = Tokens(width - 4, lengths, uppercase: false);
        return inside is null ? null : "{ " + inside + " }";
    }

    /// <summary>A braced initialiser holding one string literal.</summary>
    static string? BracedLiteral(int width) => width < 10 ? null : "{ \"" + new string('Z', width - 8) + "\" }";

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

        return probe.Outer.Contains(resume)
            ? Outcome.Outer
            : probe.InnerSpan.Contains(resume)
                ? Outcome.Inner
                : probe.Third?.Contains(resume) == true
                    ? Outcome.Third
                    : Outcome.Other;
    }

    static char Code(Outcome outcome) =>
        outcome switch {
            Outcome.Flat => 'F',
            Outcome.Outer => 'O',
            Outcome.Inner => 'I',
            Outcome.Third => 'T',
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

        // Breaking the inner construct puts everything before it on the head line, so that head is as
        // wide as the column its first continuation resumes at. The narrowest inner width where that
        // lands inside the margin is where "break the thing that overflowed" starts being enough.
        var sufficient = outcomes
            .Where(entry => (construct.Depth * Indent) + entry.Probe.Head <= Margin)
            .Select(static entry => (int?)entry.Probe.Inner)
            .FirstOrDefault();

        return new Row(
            construct.Id,
            construct.Divergence,
            first.Filler,
            first.Total,
            innerFrom,
            codes.ToString(),
            sufficient
        );
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

    /// <summary>One row of the grid, read as a boundary rather than as a string of codes.</summary>
    /// <param name="Threshold">
    ///     The narrowest inner construct at which the oracle declines the outer break having taken it one
    ///     column narrower, or nothing when the row never crosses that way.
    /// </param>
    /// <param name="Crossings">How many times the answer changes across the row. More than one is not monotone.</param>
    sealed record Reading(
        string Construct,
        string Filler,
        int Total,
        int? Threshold,
        int Crossings,
        bool AnyOuter,
        bool AnyInner,
        int Third,
        int? Sufficient) {
        public static Reading Of(Row row) {
            int? threshold = null;
            var crossings = 0;
            var anyOuter = false;
            var anyInner = false;
            var third = 0;
            char? previous = null;
            for (var i = 0; i < row.Codes.Length; i++) {
                var code = row.Codes[i];
                if (code == 'T') {
                    third++;
                }

                if (code is not ('O' or 'I')) {
                    continue;
                }

                anyOuter |= code == 'O';
                anyInner |= code == 'I';
                if (previous is not null && previous != code) {
                    crossings++;
                    if (code == 'I' && threshold is null) {
                        threshold = row.InnerFrom + i;
                    }
                }

                previous = code;
            }

            return new Reading(
                row.Construct,
                row.Filler,
                row.Total,
                threshold,
                crossings,
                anyOuter,
                anyInner,
                third,
                row.Sufficient
            );
        }

        /// <summary>The threshold as it appears in the table, carrying its own caveat.</summary>
        public string Cell =>
            (Threshold is { } value
                ? value.ToString(CultureInfo.InvariantCulture)
                : AnyInner
                    ? "all"
                    : AnyOuter
                        ? "—"
                        : "third")
            + (Crossings > 1 ? " ⚠" : string.Empty)
            + (Third > 0 && (AnyOuter || AnyInner) ? " ·" : string.Empty);
    }

    /// <summary>What the rows of one construct say, computed rather than asserted.</summary>
    /// <remarks>
    ///     ⚠ Every sentence here is derived from the grid at render time. Prose typed beside a table goes
    ///     stale the first time the table is regenerated and nobody notices; prose computed from it
    ///     cannot.
    /// </remarks>
    static string Findings(
        ConstructNote construct,
        List<Reading> readings,
        List<Row> rows,
        HashSet<string> sampled
    ) {
        var builder = new StringBuilder();
        var crossing = readings.Where(static reading => reading.Threshold is not null).ToList();
        var jagged = readings.Where(static reading => reading.Crossings > 1).ToList();

        builder.Append("Rows: ")
            .Append(readings.Count.ToString(CultureInfo.InvariantCulture))
            .Append(". Rows with a threshold in range: ")
            .Append(crossing.Count.ToString(CultureInfo.InvariantCulture))
            .Append(". Rows that cross more than once: ")
            .Append(jagged.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(".");

        if (construct.ThirdBreak is { } third) {
            var withThird = readings.Count(static reading => reading.Third > 0);
            var onlyThird = readings.Count(static reading =>
                reading.Third > 0 && !reading.AnyOuter && !reading.AnyInner
            );

            builder.AppendLine();
            builder.Append("The third break — ")
                .Append(third)
                .Append(" — appears in ")
                .Append(withThird.ToString(CultureInfo.InvariantCulture))
                .Append(" rows and is the *only* thing the oracle does in ")
                .Append(onlyThird.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" of them.");
            if (onlyThird > 0) {
                builder.AppendLine(
                    "⚠ In those rows \"which of the two constructs gives\" has no answer, because neither"
                );
                builder.AppendLine("does. Any model fitted only to the rows where one of them wins is fitted to a");
                builder.AppendLine("selected slice of the oracle's behaviour.");
            }
        }

        // ⚠ No early return when nothing crosses. A construct whose answer turns with the *total*
        // rather than within a row has no threshold anywhere in its grid and is still perfectly
        // decided — and it is the construct the model below fits best, so bailing out here would have
        // hidden the one exact result in the artefact behind "no boundary to reconstruct".
        if (crossing.Count == 0) {
            builder.AppendLine();
            builder.AppendLine(
                "The oracle never changes its mind *within* a row: whichever construct gives is settled"
            );
            builder.AppendLine("before the inner width is consulted at all, and what moves the answer is the total.");
        }

        foreach (var group in crossing.GroupBy(static reading => reading.Filler)
                     .OrderBy(
                         static group => group.Key,
                         StringComparer.Ordinal
                     )) {
            var ordered = group.OrderBy(static reading => reading.Total).ToList();

            // ⚠ Against the *last non-zero* direction, not against the previous step. The recorded
            // curve falls to a plateau and then rises off it, so every turn in it is separated from
            // the fall by a run of equal values — comparing adjacent steps counts zero turns and
            // reports a monotone boundary that is not there.
            var turns = 0;
            var direction = 0;
            for (var i = 1; i < ordered.Count; i++) {
                var step = Math.Sign(ordered[i].Threshold!.Value - ordered[i - 1].Threshold!.Value);
                if (step == 0) {
                    continue;
                }

                if (direction != 0 && step != direction) {
                    turns++;
                }

                direction = step;
            }

            var thresholds = ordered.Select(static reading => reading.Threshold!.Value).ToList();
            var heads = ordered.Select(static reading => reading.Total - reading.Threshold!.Value).ToList();
            builder.AppendLine();
            builder.Append("- `")
                .Append(group.Key)
                .Append("`: threshold ")
                .Append(thresholds.Min().ToString(CultureInfo.InvariantCulture))
                .Append('…')
                .Append(thresholds.Max().ToString(CultureInfo.InvariantCulture))
                .Append(" over totals ")
                .Append(ordered[0].Total.ToString(CultureInfo.InvariantCulture))
                .Append('…')
                .Append(ordered[^1].Total.ToString(CultureInfo.InvariantCulture))
                .Append(", turning direction ")
                .Append(turns.ToString(CultureInfo.InvariantCulture))
                .Append(turns == 1 ? " time" : " times")
                .Append(". `total − threshold` spans ")
                .Append(heads.Min().ToString(CultureInfo.InvariantCulture))
                .Append('…')
                .Append(heads.Max().ToString(CultureInfo.InvariantCulture))
                .Append(heads.Distinct().Count() == 1 ? " — **constant**" : string.Empty)
                .AppendLine(".");
        }

        builder.AppendLine();
        var agreements = crossing.Where(reading => sampled.Contains(reading.Filler))
            .GroupBy(static reading => reading.Total)
            .Where(static group => group.Count() > 1)
            .ToList();

        var unanimous = agreements.Count(static group =>
            group.Select(static reading => reading.Threshold).Distinct().Count() == 1
        );

        builder.Append("The word-length profiles agree on the threshold at ")
            .Append(unanimous.ToString(CultureInfo.InvariantCulture))
            .Append(" of the ")
            .Append(agreements.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" totals where more than one of them has a threshold to compare —");
        builder.AppendLine(
            agreements.Count > 0 && unanimous == agreements.Count
                ? "unanimously. The boundary is a fact about the oracle and the width, not about how many"
                : "which is not unanimous. Where they disagree the boundary is partly a fact about how many"
        );
        builder.AppendLine(
            agreements.Count > 0 && unanimous == agreements.Count
                ? "identifiers the probe happened to fit inside the construct."
                : "identifiers the probe fitted inside the construct, and those rows are the probe's, not"
                + " the oracle's."
        );

        if (jagged.Count > 0) {
            builder.AppendLine();
            builder.Append("⚠ ")
                .Append(jagged.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" rows cross more than once and are marked in the table above.");
        }

        builder.Append(Model(rows));
        return builder.ToString();
    }

    /// <summary>
    ///     Scores the two-term model against one construct's rows, and grades what is left.
    /// </summary>
    /// <remarks>
    ///     ⚠ The one closed form worth testing, tested rather than argued: "break the inner construct
    ///     exactly when breaking it is enough on its own, and reach further out when it is not." It is
    ///     a sentence a person can hold, so where it holds the divergence needs no oracle at all.
    ///     <para>
    ///         ⚠ Scored per <em>cell</em>, not per threshold. A construct can obey the rule perfectly and
    ///         have no threshold anywhere in its grid — that is what happens when the rule's answer
    ///         changes with the total rather than within a row — and a score that only counts crossings
    ///         reports 0 of 0 for the one construct the rule fits exactly.
    ///     </para>
    /// </remarks>
    static string Model(List<Row> rows) {
        var builder = new StringBuilder();
        var decided = 0;
        var agreed = 0;
        foreach (var row in rows) {
            for (var i = 0; i < row.Codes.Length; i++) {
                if (row.Codes[i] == '.') {
                    continue;
                }

                decided++;
                var predictsInner = row.Sufficient is { } enough && row.InnerFrom + i >= enough;
                if (predictsInner == (row.Codes[i] == 'I')) {
                    agreed++;
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("### What decides it, tested");
        builder.AppendLine();
        builder.AppendLine("**The margin law** — *break the inner construct exactly when breaking it brings the head");
        builder.AppendLine(
            "line within the margin, and reach further out when it does not* — predicts **"
            + agreed.ToString(CultureInfo.InvariantCulture)
            + " of "
            + decided.ToString(CultureInfo.InvariantCulture)
            + "** decided"
        );
        builder.Append("cells, ")
            .Append((100.0 * agreed / Math.Max(1, decided)).ToString("0.00", CultureInfo.InvariantCulture))
            .AppendLine(" %. It carries no fitted number and needs no oracle to state.");
        builder.AppendLine();
        builder.AppendLine(
            "**The margin law with a floor** — the same, and additionally the inner construct must be at"
        );
        builder.AppendLine(
            "least `F` columns wide on its own — is fitted below. `F` is one constant per shape, and it"
        );
        builder.AppendLine("is the only thing here a later reader cannot derive without measuring:");
        builder.AppendLine();
        builder.AppendLine("| filler | `F` | law alone | law with floor |");
        builder.AppendLine("|---|---:|---:|---:|");

        var scores = new List<double>();
        foreach (var filler in rows.Select(static row => row.Filler)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(static filler => filler, StringComparer.Ordinal)) {
            var mine = rows.Where(row => row.Filler == filler).ToList();
            var (floor, withFloor, plain, total) = Floor(mine);
            if (total == 0) {
                continue;
            }

            scores.Add(100.0 * withFloor / total);
            builder.Append("| `")
                .Append(filler)
                .Append("` | ")
                .Append(floor.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append((100.0 * plain / total).ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" % | ")
                .Append((100.0 * withFloor / total).ToString("0.00", CultureInfo.InvariantCulture))
                .AppendLine(" % |");
        }

        if (scores.Count == 0) {
            return builder.ToString();
        }

        // ⚠ Graded on the *worst* filler profile, and the range is printed beside the grade. A model
        // scored on its best content shape is a model scored on the content shape that suits it, and
        // this artefact exists because a previous finding here was exactly that.
        var worst = scores.Min();
        builder.AppendLine();
        builder.Append("Across the filler profiles the two-term model scores ")
            .Append(worst.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(" % to ")
            .Append(scores.Max().ToString("0.00", CultureInfo.InvariantCulture))
            .AppendLine(" %, graded here on the worst.");
        builder.AppendLine();
        builder.AppendLine(
            worst >= 99.9
            ? "⚠ **This construct is a rule, not a preference.** Two terms, one of them a single"
            + " constant,\nreproduce the oracle across the whole grid at every content shape"
            + " swept. Nothing here has\nto survive in a table — it survives in a sentence."
            : worst >= 97.0
                ? "⚠ **A rule plus a wander.** Two terms reproduce nearly every cell; what is left"
                + " is the\nboundary moving a few columns either side of `F` as the total changes."
                + " That wander is\nthe genuinely preferential part, and it is in the grid below"
                + " and nowhere else."
                : "⚠ **A rule for some content shapes and not others.** The floor is not one"
                + " constant here —\nit moves with what the inner construct is made of, so the"
                + " model closes some rows and\nleaves others open. The grid is the only record of"
                + " the ones it leaves open."
        );

        return builder.ToString();
    }

    /// <summary>
    ///     Fits the one free constant in the two-term model: how wide the inner construct must be on its
    ///     own before the oracle will break it at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ Fitted by sweeping every candidate rather than solved, because the residual is not convex —
    ///     the boundary wanders either side of the best constant instead of sitting on one side of it,
    ///     and a solver that assumed otherwise would return an endpoint.
    /// </remarks>
    static (int Floor, int WithFloor, int Plain, int Total) Floor(List<Row> rows) {
        var cells = new List<(int Inner, bool Enough, bool Measured)>();
        foreach (var row in rows) {
            for (var i = 0; i < row.Codes.Length; i++) {
                if (row.Codes[i] == '.') {
                    continue;
                }

                var inner = row.InnerFrom + i;
                cells.Add((inner, row.Sufficient is { } enough && inner >= enough, row.Codes[i] == 'I'));
            }
        }

        if (cells.Count == 0) {
            return (0, 0, 0, 0);
        }

        var plain = cells.Count(static cell => cell.Enough == cell.Measured);
        var best = (Floor: 0, Score: -1);
        for (var floor = 0; floor <= 120; floor++) {
            var score = cells.Count(cell => (cell.Enough && cell.Inner >= floor) == cell.Measured);
            if (score > best.Score) {
                best = (floor, score);
            }
        }

        return (best.Floor, best.Score, plain, cells.Count);
    }

    /// <summary>
    ///     Names the shape of an answer the probe could not classify: how many lines it has, and where in
    ///     the flat text the second one resumes relative to the landmarks the construct does know.
    /// </summary>
    static string Shape(Probe probe, string text) {
        var lines = text.Split(" ⏎ ", StringSplitOptions.None);
        if (lines.Length < 2) {
            return "single/" + lines.Length.ToString(CultureInfo.InvariantCulture);
        }

        var head = lines[0].Trim();
        if (!probe.Flat.StartsWith(head, StringComparison.Ordinal)) {
            return "unmatched";
        }

        var resume = head.Length;
        if (resume < probe.Flat.Length && probe.Flat[resume] == ' ') {
            resume++;
        }

        return (resume - probe.Outer.From).ToString(CultureInfo.InvariantCulture)
            + "/"
            + (resume - probe.InnerSpan.From).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Turns a joined group of lines back into a code block's worth of text.</summary>
    static string Unfold(string text) =>
        string.Join(
            '\n',
            text.Split(" ⏎ ", StringSplitOptions.None).Select(static line => line.TrimEnd())
        );

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
        builder.AppendLine("it different from every other open divergence: it cannot be settled after the oracle is");
        builder.AppendLine(
            "uninstalled. There is no principle to appeal to, only measurement, and the instrument goes"
        );
        builder.AppendLine("away. This file is the measurement, taken while it was still there.");
        builder.AppendLine();
        builder.AppendLine(
            "⚠ **And the measurement mostly refutes the premise, which is the best outcome available.**"
        );
        builder.AppendLine("Each construct below is scored against a two-term model: *break the inner construct when");
        builder.AppendLine("breaking it brings the head line within the margin, and when the inner construct is at");
        builder.AppendLine("least `F` columns wide on its own; otherwise take the outer break.* The first term is a");
        builder.AppendLine("sentence anyone can state without ReSharper installed. The second is one constant per");
        builder.AppendLine("shape, and it is the entire irreducible content of the \"preference fact\" — read the");
        builder.AppendLine("fitted `F` and the accuracy beside it in each construct's section.");
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

        builder.Append("## The ")
            .Append(artefact.Constructs.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" constructs");
        builder.AppendLine();
        builder.AppendLine("| id | divergence | template | outer break | inner construct | third break |");
        builder.AppendLine("|---|---|---|---|---|---|");
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
                .Append(" | ")
                .Append(construct.ThirdBreak ?? "—")
                .AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("In every construct the total width is held fixed and moved one column at a time out of an");
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

        var readings = artefact.Grid.Select(Reading.Of).ToList();

        builder.AppendLine();
        builder.AppendLine("## Where the answer flips, to the column");
        builder.AppendLine();
        builder.AppendLine("⚠ **The threshold is the finding.** A table of outputs without the boundary marked leaves");
        builder.AppendLine("the next reader to re-derive it.");
        builder.AppendLine();
        builder.AppendLine("**threshold** is the narrowest inner construct at which the oracle stops taking the outer");
        builder.AppendLine(
            "break, having taken it one column narrower. `—` means it took the outer break at every width"
        );
        builder.AppendLine("swept; `all` means it broke the inner construct at every width. A `⚠` marks a row that");
        builder.AppendLine(
            "crosses back — the answer is not monotone in the inner width, so no bisection over that row"
        );
        builder.AppendLine("would have found the boundary.");
        builder.AppendLine();
        builder.AppendLine(
            "⚠ **agree?** compares only the profiles that differ in *word lengths* and nothing else, which"
        );
        builder.AppendLine(
            "is the question that has refuted findings here before: a threshold that moves when the filler's"
        );
        builder.AppendLine("identifiers change length is a fact about the probe. `single-literal` is excluded from it");
        builder.AppendLine("because it changes the construct's *contents* — one element instead of several — and is a");
        builder.AppendLine("different measurement rather than a different sample of the same one.");
        builder.AppendLine();

        // The profiles that vary only the filler's word lengths — the ones whose disagreement would
        // mean the boundary is the probe's rather than the oracle's.
        var sampled = artefact.Fillers
            .Where(static filler => filler.TokenLengths.Count > 0)
            .Select(static filler => filler.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var construct in artefact.Constructs) {
            Section(
                builder,
                artefact,
                construct,
                [.. readings.Where(r => r.Construct == construct.Id)],
                sampled
            );
        }

        Reversals(builder, artefact);
        UnnamedCells(builder, artefact);
        Grid(builder, artefact);
        return builder.ToString();
    }

    /// <summary>One construct's threshold table, its fitted model, and the two lines at its boundary.</summary>
    static void Section(
        StringBuilder builder,
        Artefact artefact,
        ConstructNote construct,
        List<Reading> mine,
        HashSet<string> sampled
    ) {
        {
            builder.Append("### `").Append(construct.Id).Append("` — ").AppendLine(construct.Divergence);
            builder.AppendLine();
            if (mine.Count == 0) {
                builder.AppendLine("Nothing was generated for this construct.");
                builder.AppendLine();
                return;
            }

            var columns = mine.Select(static reading => reading.Filler)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static filler => filler, StringComparer.Ordinal)
                .ToList();

            builder.Append("| total |");
            foreach (var column in columns) {
                builder.Append(" `").Append(column).Append("` |");
            }

            builder.AppendLine(" agree? |");
            builder.Append("|---:|");
            foreach (var unused in columns) {
                builder.Append("---:|");
            }

            builder.AppendLine("---|");

            foreach (var total in mine.Select(static reading => reading.Total).Distinct().Order()) {
                builder.Append("| ").Append(total.ToString(CultureInfo.InvariantCulture)).Append(" |");
                var seen = new List<int>();
                foreach (var column in columns) {
                    var reading = mine.FirstOrDefault(entry => entry.Total == total && entry.Filler == column);
                    if (reading?.Threshold is { } threshold && sampled.Contains(column)) {
                        seen.Add(threshold);
                    }

                    builder.Append(' ').Append(reading is null ? "·" : reading.Cell).Append(" |");
                }

                builder.AppendLine(seen.Distinct().Count() <= 1 ? " yes |" : " **no** |");
            }

            builder.AppendLine();
            builder.AppendLine(
                Findings(
                    construct,
                    mine,
                    [.. artefact.Grid.Where(row => row.Construct == construct.Id)],
                    sampled
                )
            );
            builder.AppendLine();

            var exemplar = artefact.Flips.FirstOrDefault(flip =>
                flip.Construct == construct.Id && flip.From == "Outer" && flip.To == "Inner"
            );

            if (exemplar is not null) {
                builder.Append("The boundary itself, at total ")
                    .Append(exemplar.Total.ToString(CultureInfo.InvariantCulture))
                    .Append(" under `")
                    .Append(exemplar.Filler)
                    .AppendLine("`. One column of the inner construct separates these two:");
                builder.AppendLine();
                builder.Append("```csharp\n// inner ")
                    .Append(exemplar.Before.ToString(CultureInfo.InvariantCulture))
                    .Append(" — the oracle takes the `")
                    .Append(construct.OuterBreak)
                    .AppendLine("`");
                builder.AppendLine(Unfold(exemplar.BeforeText));
                builder.Append("\n// inner ")
                    .Append(exemplar.After.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" — one column wider, and it breaks the inner construct instead");
                builder.AppendLine(Unfold(exemplar.AfterText));
                builder.AppendLine("```");
                builder.AppendLine();
            }
        }
    }

    /// <summary>Every place a *wider* inner construct brings the outer break back.</summary>
    static void Reversals(StringBuilder builder, Artefact artefact) {
        var reversals = artefact.Flips
            .Where(static flip => flip.From == "Inner" && flip.To == "Outer")
            .ToList();

        builder.AppendLine("## Every crossing back");
        builder.AppendLine();
        if (reversals.Count == 0) {
            builder.AppendLine(
                "None. Within a row the oracle's answer changes at most once, from taking the outer break"
            );
            builder.AppendLine("to declining it, so each row *is* locally monotone in the inner width — the");
            builder.AppendLine("non-monotonicity this artefact records lives entirely in the other axis, in how the");
            builder.AppendLine("threshold moves with the total.");
        } else {
            builder.Append("⚠ ")
                .Append(reversals.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" places where a **wider** inner construct brings the outer break back. Each one is a");
            builder.AppendLine("row a bisection over the inner width would have reported the wrong boundary for.");
            builder.AppendLine();
            builder.AppendLine("| construct | filler | total | last inner | first outer |");
            builder.AppendLine("|---|---|---:|---:|---:|");
            foreach (var flip in reversals) {
                builder.Append("| `")
                    .Append(flip.Construct)
                    .Append("` | `")
                    .Append(flip.Filler)
                    .Append("` | ")
                    .Append(flip.Total.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(flip.Before.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(flip.After.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" |");
            }
        }

        builder.AppendLine();
    }

    /// <summary>The cells the probe could not name, kept verbatim rather than counted.</summary>
    static void UnnamedCells(StringBuilder builder, Artefact artefact) {
        builder.AppendLine("## Cells the probe could not name");
        builder.AppendLine();
        if (artefact.Unnamed.Count == 0) {
            builder.AppendLine(
                "None. Every cell in the grid is one of the break points the constructs name, so nothing"
            );
            builder.AppendLine("here is being averaged away.");
        } else {
            builder.AppendLine("⚠ An outcome a probe cannot name looks like noise and is usually a break point the");
            builder.AppendLine(
                "experiment did not know about. One exemplar per distinct rendering, with how many cells"
            );
            builder.AppendLine("it covers:");
            builder.AppendLine();
            foreach (var entry in artefact.Unnamed) {
                builder.Append('`')
                    .Append(entry.Construct)
                    .Append("` × `")
                    .Append(entry.Filler)
                    .Append("`, ")
                    .Append(entry.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(entry.Count == 1 ? " cell" : " cells; shown at total ")
                    .Append(entry.Count == 1 ? string.Empty : entry.Total.ToString(CultureInfo.InvariantCulture))
                    .Append(entry.Count == 1 ? string.Empty : ", inner ")
                    .Append(entry.Count == 1 ? string.Empty : entry.Inner.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(":");
                builder.AppendLine();
                builder.AppendLine("```csharp");
                builder.AppendLine(Unfold(entry.Text));
                builder.AppendLine("```");
                builder.AppendLine();
            }
        }

        builder.AppendLine();
    }

    /// <summary>The measurement itself, one character per cell.</summary>
    static void Grid(StringBuilder builder, Artefact artefact) {
        builder.AppendLine("## The grid");
        builder.AppendLine();
        builder.AppendLine("One character per inner width, left to right, starting at the row's `inner from`. The raw");
        builder.AppendLine("form of the same thing is in the JSON.");
        builder.AppendLine();

        foreach (var group in artefact.Grid.GroupBy(static row => (row.Construct, row.Filler))) {
            builder.Append("### `")
                .Append(group.Key.Construct)
                .Append("` × `")
                .Append(group.Key.Filler)
                .AppendLine("`");
            builder.AppendLine();
            builder.AppendLine("| total | inner from | enough alone | outcome by inner width |");
            builder.AppendLine("|---:|---:|---:|---|");
            foreach (var row in group) {
                builder.Append("| ")
                    .Append(row.Total.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(row.InnerFrom.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(row.Sufficient?.ToString(CultureInfo.InvariantCulture) ?? "—")
                    .Append(" | `")
                    .Append(row.Codes)
                    .AppendLine("` |");
            }

            builder.AppendLine();
        }
    }
}
