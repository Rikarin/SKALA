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
    /// <param name="FillerLeftOfOuterBreak">
    ///     ⚠ True for the two shapes whose right-hand side <em>is</em> the inner construct, so the only
    ///     inert filler available sits on the other side of the break under test.
    /// </param>
    /// <remarks>
    ///     ⚠ <paramref name="FillerLeftOfOuterBreak" /> is the defect that makes
    ///     <c>docs/sk-div-0005-margin-sweep.md</c> unable to answer this question, carried here as a
    ///     label rather than as a silent difference. When the filler sits to the left of the outer break,
    ///     widening the inner construct also widens the outer break's <em>own</em> continuation line, so
    ///     a boundary found that way confounds "the inner break is now enough" with "the outer break has
    ///     stopped being enough". Where the filler sits to the right, the whole right-hand side keeps one
    ///     width along a row and only the head line moves, which is what isolates the law's term.
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
        string? ThirdName = null,
        bool FillerLeftOfOuterBreak = false);

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
        IReadOnlyList<Unnamed> Unnamed,
        IReadOnlyList<Exemplar>? Exemplars = null);

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

    /// <summary>
    ///     One rendering the oracle actually produced, kept per construct per outcome.
    /// </summary>
    /// <remarks>
    ///     ⚠ Added because a construct can be entirely decided and still leave the reader with nothing to
    ///     look at. <c>member-chain</c> answers every cell of its grid by breaking the chain, so it has no
    ///     flip, and version 1's markdown would have printed a threshold table of dashes and no output at
    ///     all. A grid of codes whose codes are never shown is a grid nobody can check.
    /// </remarks>
    public sealed record Exemplar(
        string Construct,
        string Filler,
        int Total,
        int Inner,
        string Outcome,
        string Text);

    public sealed record ConstructNote(
        string Id,
        string Divergence,
        string Template,
        string OuterBreak,
        string InnerConstruct,
        string? ThirdBreak = null,
        bool FillerLeftOfOuterBreak = false);

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
        ),

        // ⚠ Everything below this line is a shape `docs/sk-div-0005-margin-sweep.md` named and this
        // sweep did not, rebuilt so that the filler sits on the *same* side of the break under test as
        // the inner construct. That is the whole difference between the two experiments, and it is why
        // the margin sweep's committed numbers cannot be read as a floor: see `FillerLeftOfOuterBreak`.
        new(
            "call-member",
            "SK-DIV-0005",
            "=",
            "the right-hand side's argument list",
            "var value = Utility.<name>(<args>);",
            static (name, inner) => {
                const string head = "var value = Utility.";
                var flat = head + name + inner + ";";
                var open = head.Length + name.Length;
                // ⚠ The `.` is a break point of its own and the margin sweep's `call-identifier` shape
                // has one where its `eq` sibling does not. Named, so that a dot break is not binned.
                return new Layout(
                    flat,
                    Span.Point("var value = ".Length),
                    new Span(open + 1, open + inner.Length),
                    Span.Point("var value = Utility".Length),
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2,
            "the `.` of the qualifier"
        ),
        new(
            "cast-call",
            "SK-DIV-0005",
            "=",
            "the right-hand side's argument list, behind a cast",
            "var value = (I<name>)service.Resolve(<args>);",
            static (name, inner) => {
                const string head = "var value = (I";
                const string middle = ")service.Resolve";
                var flat = head + name + middle + inner + ";";
                var close = head.Length + name.Length;
                var open = close + middle.Length;
                // ⚠ Everything between the cast's `)` and the call's `(` is one span: the oracle can
                // resume after the cast or before the `.`, and both are "declined the two the
                // divergence is about" rather than two different findings.
                return new Layout(
                    flat,
                    Span.Point("var value = ".Length),
                    new Span(open + 1, open + inner.Length),
                    new Span(close + 1, open + 1),
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2,
            "after the cast, or before the `.`"
        ),
        new(
            "generic-call",
            "SK-DIV-0005",
            "=",
            "the right-hand side's argument list, behind a type argument list",
            "var value = Deserialize<<name>>(<args>);",
            static (name, inner) => {
                const string head = "var value = Deserialize<";
                var flat = head + name + ">" + inner + ";";
                var open = head.Length + name.Length + 1;
                return new Layout(
                    flat,
                    Span.Point("var value = ".Length),
                    new Span(open + 1, open + inner.Length),
                    new Span(head.Length, head.Length + name.Length),
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2,
            "the type argument list"
        ),
        new(
            "object-initializer",
            "SK-DIV-0005",
            "=",
            "the right-hand side's object initialiser",
            "var value = new <name> { <members> };",
            static (name, inner) => {
                const string head = "var value = new ";
                var flat = head + name + " " + inner + ";";
                var open = head.Length + name.Length + 1;
                // ⚠ `{ A = 1` — as in `eq-array`, the head ends at the brace and the break eats the
                // space after it, so the head is one column narrower than where the continuation
                // resumes.
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
            "lambda-argument",
            "SK-DIV-0005",
            "=",
            "the argument list of a call inside a lambda argument",
            "var value = Assert.Throws(() => <name>(<args>));",
            static (name, inner) => {
                const string head = "var value = Assert.Throws(() => ";
                var flat = head + name + inner + ");";
                var open = head.Length + name.Length;
                // ⚠ The outer call's own list and the `=>` are one span for the same reason as the
                // cast's: each is the oracle declining both of the two under test.
                return new Layout(
                    flat,
                    Span.Point("var value = ".Length),
                    new Span(open + 1, open + inner.Length),
                    new Span("var value = Assert".Length, head.Length + 1),
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2,
            "the outer call's own list, or the `=>`"
        ),
        new(
            "member-chain",
            "SK-DIV-0005",
            "=",
            "the last call's argument list, at the end of a chain",
            "var value = source.Select(<name>).Where(<args>);",
            static (name, inner) => {
                const string head = "var value = source.Select(";
                const string middle = ").Where";
                var flat = head + name + middle + inner + ";";
                var open = head.Length + name.Length + middle.Length;
                return new Layout(
                    flat,
                    Span.Point("var value = ".Length),
                    new Span(open + 1, open + inner.Length),
                    new Span("var value = source".Length, open),
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2,
            "a `.` of the chain, or the first call's own list"
        ),
        new(
            "binary-chain",
            "SK-DIV-0005",
            "=",
            "the operand chain, broken at a `+`",
            "var value = <name> + <operands>;",
            static (name, inner) => {
                const string head = "var value = ";
                var flat = head + name + " " + inner + ";";
                var open = head.Length + name.Length;
                // ⚠ `wrap_before_binary_opsign = true` in the export, so a continuation begins *at* an
                // operator rather than after one, and the span is every column the chain occupies —
                // the oracle chooses which `+` runs out of room, exactly as it fills a type parameter
                // list.
                return new Layout(
                    flat,
                    Span.Point(head.Length),
                    new Span(open + 1, open + 1 + inner.Length),
                    null,
                    open
                );
            },
            static bodies => Body(bodies),
            2
        ),
        new(
            "ternary",
            "SK-DIV-0005",
            "=",
            "the conditional's branches, broken at `?` or `:`",
            "var value = <name> ? <then> : <else>;",
            static (name, inner) => {
                const string head = "var value = ";
                var flat = head + name + " " + inner + ";";
                var open = head.Length + name.Length;
                return new Layout(
                    flat,
                    Span.Point(head.Length),
                    new Span(open + 1, open + 1 + inner.Length),
                    null,
                    open
                );
            },
            static bodies => Body(bodies),
            2
        ),

        // ⚠ The last two shapes have no inert filler on the right of the `=` at all: their right-hand
        // side *is* the inner construct. They are swept with the filler on the left — the margin
        // sweep's design — and labelled, rather than left unmeasured or quietly mixed in with the rest.
        new(
            "collection-expression",
            "SK-DIV-0005",
            "=",
            "the collection expression itself",
            "var <name> = [<elements>];",
            static (name, inner) => {
                var flat = "var " + name + " = " + inner + ";";
                var open = "var ".Length + name.Length + 3;
                // ⚠ The third break is the gap after `var`, and it is the confound arriving in person:
                // with the filler on the left it is the *variable name* that grows, and past a certain
                // width the oracle wraps the declaration rather than either construct under test.
                return new Layout(
                    flat,
                    Span.Point(open),
                    new Span(open + 1, open + inner.Length),
                    Span.Point("var ".Length),
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2,
            "the gap after `var`, before the name",
            true
        ),
        new(
            "array-initializer",
            "SK-DIV-0005",
            "=",
            "the implicit array's initialiser",
            "var <name> = new[] { <elements> };",
            static (name, inner) => {
                var flat = "var " + name + " = new[] " + inner + ";";
                var equals = "var ".Length + name.Length + 3;
                var open = equals + "new[] ".Length;
                return new Layout(
                    flat,
                    Span.Point(equals),
                    new Span(open + 2, open + inner.Length - 1),
                    Span.Point("var ".Length),
                    open + 1
                );
            },
            static bodies => Body(bodies),
            2,
            "the gap after `var`, before the name",
            true
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
            var exemplars = new Dictionary<string, Exemplar>(StringComparer.Ordinal);

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
                    // ⚠ First-seen rather than best-of: the files are walked in a fixed order, so this
                    // is deterministic, and an exemplar chosen by any other rule would be a choice
                    // about which cell flatters the construct.
                    if (outcome is not (Outcome.Skipped or Outcome.Other)) {
                        var slot = construct.Id + ":" + outcome;
                        if (!exemplars.ContainsKey(slot)) {
                            exemplars[slot] = new Exemplar(
                                construct.Id,
                                probe.Filler,
                                probe.Total,
                                probe.Inner,
                                outcome.ToString(),
                                text
                            );
                        }
                    }

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

                // ⚠ 2: fourteen constructs rather than four, third-break cells out of the denominator,
                // and `{ "…" }` two columns wider than version 1 built it. None of those percentages is
                // comparable with a version 1 number.
                2,
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
                            construct.ThirdName,
                            construct.FillerLeftOfOuterBreak
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
                ],
                [
                    .. exemplars.Values
                        .OrderBy(static entry => entry.Construct, StringComparer.Ordinal)
                        .ThenBy(static entry => entry.Outcome, StringComparer.Ordinal)
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
            ("array-initializer", 0) => BracedLiteral(inner),
            ("array-initializer", _) => Braced(inner, filler.TokenLengths),
            ("object-initializer", 0) => MemberLiteral(inner),
            ("object-initializer", _) => Members(inner, filler.TokenLengths),
            ("collection-expression", 0) => BracketedLiteral(inner),
            ("collection-expression", _) => Bracketed(inner, filler.TokenLengths),
            ("type-parameters", _) => TypeParameters(inner, filler.TokenLengths),

            // ⚠ A chain of one operand is not a chain and a conditional cannot hold a single literal
            // and still be one, so these two shapes have no `single-literal` row rather than a faked
            // one. Returning nothing here drops the whole row, which is what the grid should say.
            ("binary-chain", 0) => null,
            ("binary-chain", _) => Operands(inner, filler.TokenLengths),
            ("ternary", 0) => null,
            ("ternary", _) => Conditional(inner, filler.TokenLengths),
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

        // ⚠ Checked rather than trusted. A generator that is four columns out is silent, survives the
        // whole run, and moves every boundary it reports — which is the failure the `Depth` note above
        // records. Two lines of arithmetic here make it impossible.
        if (text.Length != inner || (construct.Depth * Indent) + layout.Flat.Length != total) {
            throw new InvalidOperationException(
                construct.Id
                + " × "
                + filler.Id
                + " at total "
                + total.ToString(CultureInfo.InvariantCulture)
                + ", inner "
                + inner.ToString(CultureInfo.InvariantCulture)
                + ": generated an inner construct of "
                + text.Length.ToString(CultureInfo.InvariantCulture)
                + " columns in a line of "
                + ((construct.Depth * Indent) + layout.Flat.Length).ToString(CultureInfo.InvariantCulture)
                + "."
            );
        }

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

    /// <summary>
    ///     A braced initialiser holding one string literal.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>{ "…" }</c> — six columns of delimiter and quote, not eight. Version 1 of this artefact
    ///     subtracted eight and guarded at ten, so every <c>eq-array</c> × <c>single-literal</c> cell it
    ///     recorded was two columns narrower than the width it was filed under, and that construct's
    ///     fitted floor of 29 was really 27. Nothing else was affected — the three other literal
    ///     builders check out — and nothing but the arithmetic check in <see cref="Build" /> would have
    ///     found it, which is why that check is there.
    /// </remarks>
    static string? BracedLiteral(int width) => width < 7 ? null : "{ \"" + new string('Z', width - 6) + "\" }";

    /// <summary>A collection expression of exactly <paramref name="width" /> columns.</summary>
    static string? Bracketed(int width, int[] lengths) {
        var inside = Tokens(width - 2, lengths, uppercase: false);
        return inside is null ? null : "[" + inside + "]";
    }

    /// <summary>A collection expression holding one string literal.</summary>
    static string? BracketedLiteral(int width) => width < 6 ? null : "[\"" + new string('Z', width - 4) + "\"]";

    /// <summary>
    ///     An object initialiser of exactly <paramref name="width" /> columns.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>{ Alpha = 1, Beta = 1 }</c> — assignments, not bare identifiers. Bare identifiers parse
    ///     as a *collection* initialiser, which the shape's name would then be lying about, and the
    ///     export sets <c>csharp_new_line_before_members_in_object_initializers</c>, which is a key about
    ///     the one of the two this construct is supposed to be.
    /// </remarks>
    static string? Members(int width, int[] lengths) {
        var inside = Tokens(width - 4, lengths, uppercase: true, suffix: " = 1");
        return inside is null ? null : "{ " + inside + " }";
    }

    /// <summary>An object initialiser holding one member, whose value is a string literal.</summary>
    static string? MemberLiteral(int width) =>
        // `{ Value = "…" }` — fourteen columns before any content.
        width < 15 ? null : "{ Value = \"" + new string('Z', width - 14) + "\" }";

    /// <summary>
    ///     The tail of a binary chain — <c>+ a + b</c> — of exactly <paramref name="width" /> columns.
    /// </summary>
    /// <remarks>
    ///     ⚠ It begins at the operator rather than after it, because the export sets
    ///     <c>wrap_before_binary_opsign = true</c> and the continuation therefore begins at a <c>+</c>.
    ///     Building the text the same way the break reads it is what keeps the classifier positional.
    /// </remarks>
    static string? Operands(int width, int[] lengths) {
        var inside = Tokens(width - 2, lengths, uppercase: false, separator: " + ");
        return inside is null ? null : "+ " + inside;
    }

    /// <summary>A conditional's two branches — <c>? a : b</c> — of exactly <paramref name="width" /> columns.</summary>
    static string? Conditional(int width, int[] lengths) {
        // `? a : b` — five columns of operator and padding around two identifiers.
        if (width < 7) {
            return null;
        }

        var body = width - 5;
        var then = Math.Min(lengths[0], body - 1);
        return "? " + Word(then, 0) + " : " + Word(body - then, 1);
    }

    /// <summary>A single string-literal argument filling the list on its own.</summary>
    static string? Literal(int width) =>
        // `("…")` — four columns of delimiter and quote before any content.
        width < 6 ? null : "(\"" + new string('Z', width - 4) + "\")";

    /// <summary>
    ///     Comma-separated identifiers coming to exactly <paramref name="width" /> columns, with lengths
    ///     drawn from <paramref name="lengths" /> in order and the last one trimmed to close the gap.
    /// </summary>
    /// <param name="separator">
    ///     What joins two entries. ⚠ Widened from a hard-coded <c>", "</c> so that a binary chain — whose
    ///     entries are joined by <c>" + "</c> — is generated by the same width arithmetic as an argument
    ///     list, rather than by a second copy of it that could round differently.
    /// </param>
    /// <param name="suffix">What follows each identifier, for shapes whose entries are not bare names.</param>
    static string? Tokens(int width, int[] lengths, bool uppercase, string separator = ", ", string suffix = "") {
        if (width < 1) {
            return null;
        }

        var chosen = new List<int>();
        var remaining = width;
        var index = 0;
        while (true) {
            var gap = chosen.Count == 0 ? 0 : separator.Length;
            var length = lengths[index % lengths.Length];
            index++;

            // Keep going only while a further identifier of at least one column could still close the
            // gap exactly; otherwise this one takes the remainder.
            if (remaining - gap - length - suffix.Length >= separator.Length + 1 + suffix.Length) {
                chosen.Add(length);
                remaining -= gap + length + suffix.Length;
                continue;
            }

            var last = remaining - gap - suffix.Length;
            if (last < 1) {
                return null;
            }

            chosen.Add(last);
            break;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < chosen.Count; i++) {
            if (i > 0) {
                builder.Append(separator);
            }

            var word = Word(chosen[i], i);
            builder.Append(uppercase ? char.ToUpperInvariant(word[0]) + word[1..] : word).Append(suffix);
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

        if (agreements.Count == 0) {
            builder.AppendLine("No total has a threshold under more than one word-length profile, so there is nothing");
            builder.AppendLine(
                "here to disagree about — which is itself the answer: a boundary the probe's identifier"
            );
            builder.AppendLine("lengths could have moved would have produced one.");
        } else {
            builder.Append("The word-length profiles agree on the threshold at ")
                .Append(unanimous.ToString(CultureInfo.InvariantCulture))
                .Append(" of the ")
                .Append(agreements.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" totals where more than one of them has a threshold to compare —");
            builder.AppendLine(
                unanimous == agreements.Count
                    ? "unanimously. The boundary is a fact about the oracle and the width, not about how"
                    + " many\nidentifiers the probe happened to fit inside the construct."
                    : "which is not unanimous. Where they disagree the boundary is partly a fact about how"
                    + " many\nidentifiers the probe fitted inside the construct, and those rows are the"
                    + " probe's, not the\noracle's."
            );
        }

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
        var whole = Fit.Of(rows);

        builder.AppendLine();
        builder.AppendLine("### What decides it, tested");
        builder.AppendLine();
        if (whole.Chose == 0) {
            builder.AppendLine(
                "⚠ **Nothing to score.** The oracle never took either of the two constructs under test in"
            );
            builder.AppendLine(
                "any cell of this construct's grid, so there is no boundary here and no floor to fit — what"
            );
            builder.AppendLine("it does instead is counted above and shown below.");
            return builder.ToString();
        }

        builder.AppendLine("**The margin law** — *break the inner construct exactly when breaking it brings the head");
        builder.AppendLine(
            "line within the margin, and reach further out when it does not* — predicts **"
            + whole.Plain.ToString(CultureInfo.InvariantCulture)
            + " of "
            + whole.Chose.ToString(CultureInfo.InvariantCulture)
            + "** cells"
        );
        builder.Append("the oracle answered with one of the two, ")
            .Append(whole.PlainPercent.ToString("0.00", CultureInfo.InvariantCulture))
            .AppendLine(" %. It carries no fitted number and needs no oracle to state.");
        if (whole.Third > 0 || whole.Unnamed > 0) {
            builder.AppendLine();
            builder.Append("⚠ A further ")
                .Append(whole.Third.ToString(CultureInfo.InvariantCulture))
                .Append(" cells are the oracle declining *both* of the two, and ")
                .Append(whole.Unnamed.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" are unnamed or came");
            builder.AppendLine(
                "back flat. Neither is in the denominator: a model of \"which of these two gives\" can only"
            );
            builder.AppendLine("be graded on cells where one of them did.");
        }

        builder.AppendLine();
        builder.AppendLine(
            "**The margin law with a floor** — the same, and additionally the inner construct must be at"
        );
        builder.AppendLine(
            "least `F` columns wide on its own — is fitted below. `F` is one constant per shape, and it"
        );
        builder.AppendLine("is the only thing here a later reader cannot derive without measuring:");
        builder.AppendLine();
        builder.AppendLine("| filler | cells | `F` | law alone | law with floor |");
        builder.AppendLine("|---|---:|---:|---:|---:|");

        var scores = new List<double>();
        foreach (var filler in rows.Select(static row => row.Filler)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(static filler => filler, StringComparer.Ordinal)) {
            var fit = Fit.Of([.. rows.Where(row => row.Filler == filler)]);
            if (fit.Chose == 0) {
                continue;
            }

            scores.Add(fit.FloorPercent);
            builder.Append("| `")
                .Append(filler)
                .Append("` | ")
                .Append(fit.Chose.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(fit.Floor.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(fit.PlainPercent.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" % | ")
                .Append(fit.FloorPercent.ToString("0.00", CultureInfo.InvariantCulture))
                .AppendLine(" % |");
        }

        // ⚠ The pooled row is the one a later reader wants, because `F` is claimed to be a constant per
        // *shape* and a per-filler fit cannot say whether it is: four numbers that disagree are four
        // facts about the probe's content until the fit over all of them is written down beside them.
        builder.Append("| **every filler** | ")
            .Append(whole.Chose.ToString(CultureInfo.InvariantCulture))
            .Append(" | **")
            .Append(whole.Floor.ToString(CultureInfo.InvariantCulture))
            .Append("** | ")
            .Append(whole.PlainPercent.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(" % | **")
            .Append(whole.FloorPercent.ToString("0.00", CultureInfo.InvariantCulture))
            .AppendLine(" %** |");

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
    ///     What one slice of the grid says about the two-term model, and how much of that slice the model
    ///     is entitled to be graded on.
    /// </summary>
    /// <param name="Chose">Cells where the oracle took one of the two constructs the divergence is about.</param>
    /// <param name="Third">Cells where it declined both and took the construct's third break.</param>
    /// <param name="Unnamed">Cells the probe could not name, or that came back flat — a defect either way.</param>
    /// <param name="Plain">How many of <paramref name="Chose" /> the margin law alone gets right.</param>
    /// <param name="Floor">The fitted floor: how wide the inner construct must be before the oracle breaks it.</param>
    /// <param name="WithFloor">How many of <paramref name="Chose" /> the law plus that floor gets right.</param>
    /// <remarks>
    ///     ⚠ <paramref name="Third" /> and <paramref name="Unnamed" /> are held out of the denominator
    ///     rather than scored as "not the inner construct". Version 1 of this artefact scored every
    ///     generated cell, which grades the model where the question it answers was never asked: a
    ///     construct the oracle settles by declining <em>both</em> of the two — <c>member-chain</c> does
    ///     it in every cell it has — then scores as high as the model happens to say "outer", and
    ///     <c>type-parameters</c>' 2 522 third-break cells counted as agreements. The percentages here
    ///     are therefore not comparable with version 1's and are lower wherever a construct has a third
    ///     break.
    ///     <para>
    ///         ⚠ The floor is fitted by sweeping every candidate rather than solved, because the residual
    ///         is not convex — the boundary wanders either side of the best constant instead of sitting
    ///         on one side of it, and a solver that assumed otherwise would return an endpoint.
    ///     </para>
    /// </remarks>
    sealed record Fit(int Chose, int Third, int Unnamed, int Plain, int Floor, int WithFloor) {
        public double PlainPercent => 100.0 * Plain / Math.Max(1, Chose);

        public double FloorPercent => 100.0 * WithFloor / Math.Max(1, Chose);

        public static Fit Of(List<Row> rows) {
            var cells = new List<(int Inner, bool Enough, bool Measured)>();
            var third = 0;
            var unnamed = 0;
            foreach (var row in rows) {
                for (var i = 0; i < row.Codes.Length; i++) {
                    var code = row.Codes[i];
                    if (code == '.') {
                        continue;
                    }

                    if (code == 'T') {
                        third++;
                        continue;
                    }

                    if (code is not ('O' or 'I')) {
                        unnamed++;
                        continue;
                    }

                    var inner = row.InnerFrom + i;
                    cells.Add((inner, row.Sufficient is { } enough && inner >= enough, code == 'I'));
                }
            }

            if (cells.Count == 0) {
                return new Fit(0, third, unnamed, 0, 0, 0);
            }

            var plain = cells.Count(static cell => cell.Enough == cell.Measured);
            var best = (Floor: 0, Score: -1);
            for (var floor = 0; floor <= 120; floor++) {
                var score = cells.Count(cell => (cell.Enough && cell.Inner >= floor) == cell.Measured);
                if (score > best.Score) {
                    best = (floor, score);
                }
            }

            return new Fit(cells.Count, third, unnamed, plain, best.Floor, best.Score);
        }
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

        Confound(builder, artefact);
        Summary(builder, artefact);

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

    /// <summary>
    ///     Why the shapes below had to be re-measured rather than read out of the margin sweep.
    /// </summary>
    /// <remarks>
    ///     ⚠ This section exists because the obvious economy — <c>docs/sk-div-0005-margin-sweep.md</c>
    ///     already swept ten of these shapes, so read `F` off it — is wrong, and wrong in a way that
    ///     costs nothing to state and a re-measurement to discover. Stated here rather than in a commit
    ///     message, because the next reader will have the same idea.
    /// </remarks>
    static void Confound(StringBuilder builder, Artefact artefact) {
        var left = artefact.Constructs.Where(static construct => construct.FillerLeftOfOuterBreak).ToList();

        builder.AppendLine("## Why the margin sweep's shapes are re-measured here");
        builder.AppendLine();
        builder.AppendLine("[`sk-div-0005-margin-sweep.md`](sk-div-0005-margin-sweep.md) already swept ten of the");
        builder.AppendLine("shapes below and it cannot answer this question, for four reasons that are worth writing");
        builder.AppendLine("down so nobody re-derives them:");
        builder.AppendLine();
        builder.AppendLine(
            "1. **It pads the variable name, which is on the wrong side of the break under test.** Widening"
        );
        builder.AppendLine("   the right-hand side there also widens the `=` break's *own* continuation line, so its");
        builder.AppendLine(
            "   boundary confounds \"the inner break is now enough\" with \"the outer break has stopped"
        );
        builder.AppendLine(
            "   being enough\" — and it is the second one it tracks. At a flat width of 121 its flip sits"
        );
        builder.AppendLine("   at a continuation line of exactly 112 columns at block depths 2, 3, 4, 5 and 6 alike");
        builder.AppendLine("   (its `base64-literal` row), while the head line this law asks about is 45, 49, 53, 57");
        builder.AppendLine("   and 61 columns at those same five flips — nowhere near the margin, and never twice the");
        builder.AppendLine("   same. The law's boundary is not in that data at any depth.");
        builder.AppendLine("2. **It records one number per row, not one per cell.** The cell is \"the longest");
        builder.AppendLine("   continuation line still written rather than wrapping\" — a threshold, and a maximum at");
        builder.AppendLine(
            "   that. A floor is fitted per cell and scored per cell; neither is recoverable from a row's"
        );
        builder.AppendLine("   maximum.");
        builder.AppendLine("3. **Its third bucket is `anything else`.** Breaking the inner construct, taking a third");
        builder.AppendLine("   break, and doing something the probe cannot name are one bucket there. Two of the");
        builder.AppendLine("   shapes below turn out to live almost entirely in the second of those.");
        builder.AppendLine("4. **Two totals, 121 and 137.** A floor is only separable from the law by watching the");
        builder.AppendLine("   threshold plateau across many totals while the law's own term climbs past it.");
        builder.AppendLine();

        if (left.Count > 0) {
            builder.Append("⚠ And the defect in (1) is not always avoidable: ")
                .Append(left.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" of the shapes below —");
            builder.AppendLine(
                string.Join(", ", left.Select(static construct => "`" + construct.Id + "`"))
                + " — have a right-hand side that *is* the inner"
            );
            builder.AppendLine("construct, so no inert filler exists on the same side of the `=` as the thing being");
            builder.AppendLine(
                "swept. They are measured with the filler on the left, the way the margin sweep measures"
            );
            builder.AppendLine(
                "everything, and marked `left` in the table below. Their `F` is not comparable with the"
            );
            builder.AppendLine("rest and is reported anyway, because a labelled number is worth more than a gap.");
            builder.AppendLine();
        }
    }

    /// <summary>
    ///     Every shape's fitted floor and the law's score on it, in one table.
    /// </summary>
    /// <remarks>
    ///     ⚠ At the top and before the per-construct sections, because `F` is the one thing in this
    ///     artefact that cannot be re-derived after the oracle is uninstalled, and a reader who has to
    ///     assemble it from fourteen sections will assemble it wrong.
    /// </remarks>
    static void Summary(StringBuilder builder, Artefact artefact) {
        builder.AppendLine("## `F` per shape");
        builder.AppendLine();
        builder.AppendLine(
            "The fitted floor, and how much of each shape the two-term model reproduces. **cells** counts"
        );
        builder.AppendLine(
            "only where the oracle took one of the two constructs under test; **third** counts where it"
        );
        builder.AppendLine("declined both, and those are not in the score. `F` is fitted over every filler profile at");
        builder.AppendLine("once — the per-profile fits are in each shape's own section, and where they disagree the");
        builder.AppendLine("disagreement is the finding.");
        builder.AppendLine();
        builder.AppendLine("| shape | divergence | filler | cells | third | `F` | law alone | law with floor |");
        builder.AppendLine("|---|---|---|---:|---:|---:|---:|---:|");

        foreach (var construct in artefact.Constructs) {
            var fit = Fit.Of([.. artefact.Grid.Where(row => row.Construct == construct.Id)]);
            builder.Append("| `")
                .Append(construct.Id)
                .Append("` | ")
                .Append(construct.Divergence)
                .Append(" | ")
                .Append(construct.FillerLeftOfOuterBreak ? "⚠ left" : "right")
                .Append(" | ")
                .Append(fit.Chose.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(fit.Third.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(fit.Chose == 0 ? "—" : fit.Floor.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(fit.Chose == 0 ? "—" : fit.PlainPercent.ToString("0.00", CultureInfo.InvariantCulture) + " %")
                .Append(" | ")
                .Append(fit.Chose == 0 ? "—" : fit.FloorPercent.ToString("0.00", CultureInfo.InvariantCulture) + " %")
                .AppendLine(" |");
        }

        builder.AppendLine();

        var fits = artefact.Constructs
            .Select(construct => (construct,
                    Fit: Fit.Of([.. artefact.Grid.Where(row => row.Construct == construct.Id)]))
            )
            .ToList();

        var scored = fits.Where(static entry => entry.Fit.Chose > 0).ToList();
        var mute = fits.Where(static entry => entry.Fit.Chose == 0).ToList();
        var closed = scored.Where(static entry => entry.Fit.FloorPercent >= 99.0).ToList();

        builder.Append("**")
            .Append(closed.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" of the ")
            .Append(scored.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" shapes where the question has an answer are reproduced to 99 % or better by");
        builder.AppendLine("the law and one constant.** The rest are where the preference actually lives, and their");
        builder.AppendLine("residue is in the grid at the end of this file and nowhere else.");

        // ⚠ The floor is fitted by sweeping candidates up to 120, so it can land *past* the widest
        // inner construct the grid holds — and when it does, "the law with a floor" is not the law
        // with a floor. It is the law switched off, and the score beside it is the share of cells the
        // oracle answered with the outer break. Flagged here because the number otherwise reads as a
        // model that works.
        var widest = artefact.Grid.Count == 0
            ? 0
            : artefact.Grid.Max(static row => row.InnerFrom + row.Codes.Length - 1);

        var degenerate = fits.Where(entry => entry.Fit.Chose > 0 && entry.Fit.Floor > widest).ToList();
        if (degenerate.Count > 0) {
            builder.AppendLine();
            builder.Append("⚠ **A fitted `F` above ")
                .Append(widest.ToString(CultureInfo.InvariantCulture))
                .Append(" is not a floor — it is the law switched off.** ")
                .Append(string.Join(", ", degenerate.Select(static entry => "`" + entry.construct.Id + "`")))
                .AppendLine(" fits a");
            builder.AppendLine("floor wider than any inner construct swept, which means the best available model of");
            builder.AppendLine(
                "that shape is \"always take the outer break\" and the percentage beside it is just how"
            );
            builder.AppendLine("often the oracle did. Every such shape is one of the `⚠ left` rows, and that is the");
            builder.AppendLine("confound in the margin sweep's design showing up as a number: with the filler on the");
            builder.AppendLine("wrong side of the break, the law's own term stops carrying anything and a fitted");
            builder.AppendLine(
                "constant absorbs the whole grid. It is the clearest evidence in this file that a floor"
            );
            builder.AppendLine("read off a sweep built that way would be an artefact of the sweep.");
        }

        if (mute.Count > 0) {
            builder.AppendLine();
            builder.Append("⚠ ")
                .Append(mute.Count.ToString(CultureInfo.InvariantCulture))
                .Append(mute.Count == 1 ? " shape — " : " shapes — ")
                .Append(string.Join(", ", mute.Select(static entry => "`" + entry.construct.Id + "`")))
                .AppendLine(" — never took either of the two");
            builder.AppendLine(
                "constructs under test in any cell swept. For those, \"which of these two gives\" has no"
            );
            builder.AppendLine("answer to record: the oracle reaches past both every time, and a floor fitted to them");
            builder.AppendLine("would be fitted to nothing.");
        }

        builder.AppendLine();
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

            // ⚠ Shown whenever the flip above did not already show it, and always for the third break.
            // A construct with no flip — because the oracle answers every cell the same way — otherwise
            // renders as a table of dashes with no output in it at all, which is a grid nobody can
            // check.
            foreach (var shown in (artefact.Exemplars ?? [])
                         .Where(entry => entry.Construct == construct.Id)
                         .Where(entry => exemplar is null || entry.Outcome is "Third" or "Flat")) {
                builder.Append("What `")
                    .Append(shown.Outcome)
                    .Append("` is, for this construct — total ")
                    .Append(shown.Total.ToString(CultureInfo.InvariantCulture))
                    .Append(", inner ")
                    .Append(shown.Inner.ToString(CultureInfo.InvariantCulture))
                    .Append(", `")
                    .Append(shown.Filler)
                    .AppendLine("`:");
                builder.AppendLine();
                builder.AppendLine("```csharp");
                builder.AppendLine(Unfold(shown.Text));
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
