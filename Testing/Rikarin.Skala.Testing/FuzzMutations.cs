using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>What a mutation is allowed to claim about its own output.</summary>
public enum MutationClass {
    /// <summary>
    ///     Whitespace that carries no information: indentation, trailing space, the width of a gap
    ///     between two tokens on one line. ⚠ The formatter must <b>absorb</b> it —
    ///     <c>format(mutate(x)) ≡ format(x)</c>, docs/plan/12 § "Fuzzing" — and that is the strongest
    ///     property the fuzzer asserts, because the preserve-and-repair model of ADR-002 makes it
    ///     genuinely hard rather than trivially true.
    /// </summary>
    Absorbed,

    /// <summary>
    ///     Parse-preserving but information-bearing: a new comment, a blank line, a moved line break, a
    ///     widened identifier, a <c>#if</c>. The output is allowed to differ from the baseline's; the
    ///     six properties still have to hold over it.
    /// </summary>
    Structural
}

/// <summary>One applied mutation.</summary>
public sealed record Mutation(string Name, MutationClass Class, string Text);

/// <summary>
///     The parse-preserving text mutations of docs/plan/12 § "Fuzzing", each seeded.
/// </summary>
/// <remarks>
///     ⚠ Every mutation here is required to keep the file parsing the way it parsed before. That is not
///     politeness: a mutation that breaks the parse produces a file the formatter refuses to touch by
///     policy (ADR-003 — reported, left byte-identical), so every property holds over it trivially and
///     the case measured nothing. The driver checks the parse afterwards and counts a mutation that
///     broke it as a *fuzzer* defect rather than a formatter one; see <see cref="Fuzzer" />.
///     <para>
///         ⚠ The protections below are the same ones <c>PropertyTests.MutateIndentationOnly</c> arrived at
///         and are the whole content of "parse-preserving" in practice: a space inside a raw string, a
///         verbatim string, an interpolation hole, a multi-line comment or a run of disabled text is
///         <b>data</b>, not whitespace, and moving it changes the program. The difference from that method
///         is that this one is a stream of many mutations driven by a seed rather than one fixed transform,
///         which is exactly the gap doc 12 § "Fuzzing" recorded.
///     </para>
/// </remarks>
public static class FuzzMutations {
    public const string Indent = "indent";
    public const string TrailingSpace = "trailing-space";
    public const string WidenGap = "widen-gap";
    public const string CollapseGap = "collapse-gap";
    public const string Tabs = "tabs";
    public const string CommentLine = "comment-line";
    public const string CommentInline = "comment-inline";
    public const string TrailingComment = "trailing-comment";
    public const string BlankLines = "blank-lines";
    public const string RemoveBlankLine = "remove-blank-line";
    public const string IfTrue = "if-true";
    public const string IfDisabled = "if-disabled";
    public const string Region = "region";
    public const string Pragma = "pragma";
    public const string LineEndings = "line-endings";
    public const string Bom = "bom";
    public const string WidenIdentifier = "widen-identifier";
    public const string JoinLine = "join-line";
    public const string SplitLine = "split-line";

    /// <summary>
    ///     The whitespace-only mutations, which the absorption property is asserted over.
    /// </summary>
    public static readonly ImmutableArray<string> AbsorbedNames = [Indent, TrailingSpace, WidenGap, CollapseGap, Tabs];

    /// <summary>
    ///     Every mutation, with the weight it is drawn at.
    /// </summary>
    /// <remarks>
    ///     ⚠ The weights are not uniform and the shape is deliberate. The absorbed five are drawn hard
    ///     because they carry the strong property; <see cref="WidenIdentifier" /> is drawn hard because
    ///     it is the only mutation that changes a line's *width*, which is the input the fitting engine
    ///     makes its decisions from — docs/plan/16 § R2's argument that the fitter is where the risk
    ///     lives is also the argument for that weight. <see cref="Bom" /> and
    ///     <see cref="LineEndings" /> are drawn softly because they are whole-file transforms with one
    ///     bit of information in them and re-drawing them adds nothing.
    /// </remarks>
    public static readonly ImmutableArray<(string Name, MutationClass Class, int Weight)> Catalogue = [
        (Indent, MutationClass.Absorbed, 10),
        (TrailingSpace, MutationClass.Absorbed, 8),
        (WidenGap, MutationClass.Absorbed, 10),
        (CollapseGap, MutationClass.Absorbed, 6),
        (Tabs, MutationClass.Absorbed, 6),
        (CommentLine, MutationClass.Structural, 7),
        (CommentInline, MutationClass.Structural, 7),
        (TrailingComment, MutationClass.Structural, 5),
        (BlankLines, MutationClass.Structural, 6),
        (RemoveBlankLine, MutationClass.Structural, 4),
        (IfTrue, MutationClass.Structural, 5),
        (IfDisabled, MutationClass.Structural, 5),
        (Region, MutationClass.Structural, 3),
        (Pragma, MutationClass.Structural, 3),
        (LineEndings, MutationClass.Structural, 2),
        (Bom, MutationClass.Structural, 1),
        (WidenIdentifier, MutationClass.Structural, 10),
        (JoinLine, MutationClass.Structural, 8),
        (SplitLine, MutationClass.Structural, 8)
    ];

    /// <summary>
    ///     Applies one mutation drawn from <paramref name="names" />, or <c>null</c> if none applied.
    /// </summary>
    /// <remarks>
    ///     ⚠ A mutation returns <c>null</c> rather than the input when it has nowhere to act — a
    ///     twelve-line file has no blank line to remove — and the driver re-draws. Returning the input
    ///     unchanged would count a case that asserted the properties over the *original* file as a
    ///     fuzz case, which is how a fuzzer reports thousands of executions and covers one input.
    /// </remarks>
    public static Mutation? Apply(
        string source,
        FuzzRandom random,
        IReadOnlyList<string> symbols,
        IReadOnlyList<string>? names = null
    ) {
        var pool = names is null
            ? Catalogue
            : [.. Catalogue.Where(entry => names.Contains(entry.Name, StringComparer.Ordinal))];

        if (pool.Length == 0) {
            return null;
        }

        var chosen = random.Pick(pool, [.. pool.Select(static entry => entry.Weight)]);
        var text = Apply(chosen.Name, source, random, symbols);
        return text is null || string.Equals(text, source, StringComparison.Ordinal)
            ? null
            : new Mutation(chosen.Name, chosen.Class, text);
    }

    public static string? Apply(string name, string source, FuzzRandom random, IReadOnlyList<string> symbols) {
        var map = SourceMap.Of(source, symbols);
        return name switch {
            Indent => Reindent(map, random),
            TrailingSpace => Trailing(map, random),
            WidenGap => WidenGaps(map, random),
            CollapseGap => CollapseGaps(map, random),
            Tabs => Tabify(map, random),
            CommentLine => InsertLines(
                map,
                random,
                static (random, indent) =>
                    indent + "// fuzz " + random.Next(1000).ToString(CultureInfo.InvariantCulture) + "\n"
            ),
            CommentInline => InsertAtGap(map, random, "/* f */"),
            TrailingComment => Trailing(map, random, " // fuzz"),
            BlankLines => InsertLines(map, random, static (random, _) => new string('\n', random.Next(1, 4))),
            RemoveBlankLine => RemoveBlank(map, random),
            IfTrue => Wrap(map, random, "#if true", "#endif"),
            Region => Wrap(map, random, "#region fuzz", "#endregion"),
            IfDisabled => InsertLines(
                map,
                random,
                static (random, _) =>
                    "#if FUZZ_NOT_DEFINED_"
                    + random.Next(1000).ToString(CultureInfo.InvariantCulture)
                    + "\n    this is not code and never will be\n#endif\n"
            ),
            Pragma => InsertLines(map, random, static (_, _) => "#pragma warning disable CS0168\n"),
            LineEndings => SwapLineEndings(map, random),
            Bom => ToggleBom(map),
            WidenIdentifier => Widen(map, random),
            JoinLine => Join(map, random),
            SplitLine => Split(map, random),
            _ => null
        };
    }

    // ── the absorbed five ────────────────────────────────────────────────────────────────────────

    /// <summary>Scales the leading whitespace of a random selection of safe lines.</summary>
    static string? Reindent(SourceMap map, FuzzRandom random) {
        var lines = map.SafeLines(true, absorbing: true);
        if (lines.Count == 0) {
            return null;
        }

        var extra = random.Next(1, 9);
        var edits = new List<(int Position, int Delete, string Insert)>();
        foreach (var line in Sample(lines, random)) {
            var span = map.Text.Lines[line];
            var content = span.ToString();
            var indent = 0;
            while (indent < content.Length && content[indent] is ' ' or '\t') {
                indent++;
            }

            // ⚠ A whitespace-only line is indented too, deliberately. A blank line carrying
            // indentation is what every editor with "keep indentation" configured writes, it is
            // still whitespace and nothing else, and it must therefore be absorbed like the rest.
            _ = indent;
            edits.Add((span.Start, 0, new string(' ', extra)));
        }

        return edits.Count == 0 ? null : Splice(map.Source, edits);
    }

    static string? Trailing(SourceMap map, FuzzRandom random, string? suffix = null) {
        // ⚠ A line whose last trivia is a comment is excluded when the suffix is whitespace. Trailing
        // spaces *inside* a `// …` are part of the comment's text, and whether a formatter is allowed
        // to trim them is a question about comment handling rather than about whitespace absorption.
        // With an explicit suffix the mutation is structural and the exclusion does not apply.
        var lines = map.SafeLines(false, suffix is null, suffix is null);
        if (lines.Count == 0) {
            return null;
        }

        var text = suffix ?? new string(' ', random.Next(1, 5));
        var edits = new List<(int Position, int Delete, string Insert)>();
        foreach (var line in Sample(lines, random)) {
            var span = map.Text.Lines[line];
            if (span.End == span.Start) {
                continue;
            }

            edits.Add((span.End, 0, text));
        }

        return edits.Count == 0 ? null : Splice(map.Source, edits);
    }

    static string? WidenGaps(SourceMap map, FuzzRandom random) {
        var gaps = map.AbsorbableGaps;
        if (gaps.Count == 0) {
            return null;
        }

        var width = random.Next(1, 4);
        var edits = Sample(gaps, random)
            .Select(gap => (gap.Start, 0, new string(' ', width)))
            .ToList();

        return edits.Count == 0 ? null : Splice(map.Source, edits);
    }

    static string? CollapseGaps(SourceMap map, FuzzRandom random) {
        // ⚠ Never to zero. `a + +b` with the gap removed re-tokenises to `++`, which is a different
        // program; a run of two or more spaces reduced to exactly one is always the same token
        // stream.
        var wide = map.AbsorbableGaps.Where(static gap => gap.End - gap.Start >= 2).ToList();
        if (wide.Count == 0) {
            return null;
        }

        var edits = Sample(wide, random)
            .Select(static gap => (gap.Start, gap.End - gap.Start, " "))
            .ToList();

        return edits.Count == 0 ? null : Splice(map.Source, edits);
    }

    static string? Tabify(SourceMap map, FuzzRandom random) {
        var lines = map.SafeLines(true, absorbing: true);
        if (lines.Count == 0) {
            return null;
        }

        var edits = new List<(int Position, int Delete, string Insert)>();
        foreach (var line in Sample(lines, random)) {
            var span = map.Text.Lines[line];
            var content = span.ToString();
            var indent = 0;
            while (indent < content.Length && content[indent] == ' ') {
                indent++;
            }

            if (indent < 2) {
                continue;
            }

            edits.Add((span.Start, indent, new string('\t', indent / 2) + new string(' ', indent % 2)));
        }

        return edits.Count == 0 ? null : Splice(map.Source, edits);
    }

    // ── the structural fourteen ──────────────────────────────────────────────────────────────────

    static string? InsertLines(SourceMap map, FuzzRandom random, Func<FuzzRandom, string, string> body) {
        var boundaries = map.LineBoundaries;
        if (boundaries.Count == 0) {
            return null;
        }

        var edits = new List<(int Position, int Delete, string Insert)>();
        foreach (var line in Sample(boundaries, random, 3)) {
            var span = map.Text.Lines[line];
            var content = span.ToString();
            var indent = 0;
            while (indent < content.Length && content[indent] is ' ' or '\t') {
                indent++;
            }

            edits.Add((span.Start, 0, body(random, content[..indent])));
        }

        return edits.Count == 0 ? null : Splice(map.Source, edits);
    }

    static string? InsertAtGap(SourceMap map, FuzzRandom random, string text) {
        if (map.Gaps.Count == 0) {
            return null;
        }

        var edits = Sample(map.Gaps, random, 4)
            .Select(gap => (gap.Start, 0, " " + text + " "))
            .ToList();

        return edits.Count == 0 ? null : Splice(map.Source, edits);
    }

    static string? RemoveBlank(SourceMap map, FuzzRandom random) {
        var blanks = map.LineBoundaries
            .Where(line => map.Text.Lines[line].ToString().Trim().Length == 0)
            .Where(line => map.Text.Lines[line].EndIncludingLineBreak > map.Text.Lines[line].End)
            .ToList();

        if (blanks.Count == 0) {
            return null;
        }

        var chosen = random.Pick(blanks);
        var span = map.Text.Lines[chosen];
        return Splice(map.Source, [(span.Start, span.EndIncludingLineBreak - span.Start, string.Empty)]);
    }

    /// <summary>Wraps a run of lines in a directive pair.</summary>
    /// <remarks>
    ///     ⚠ The run may contain no preprocessor directive of its own. `#if true` inserted before an
    ///     `#endif` whose `#if` is above the run steals that `#endif` and leaves the original one
    ///     stray, which is a *parse error* rather than a parse-preserving mutation, and a file with a
    ///     parse error is one the formatter leaves byte-identical by policy — a case that asserts
    ///     nothing.
    /// </remarks>
    static string? Wrap(SourceMap map, FuzzRandom random, string open, string close) {
        var runs = map.DirectiveFreeRuns;
        if (runs.Count == 0) {
            return null;
        }

        var (first, last) = random.Pick(runs);
        var boundaries = map.LineBoundaries.Where(line => line >= first && line <= last).ToList();
        if (boundaries.Count < 2) {
            return null;
        }

        var startIndex = random.Next(boundaries.Count - 1);
        var endIndex = startIndex + 1 + random.Next(Math.Min(12, boundaries.Count - 1 - startIndex));
        var before = map.Text.Lines[boundaries[startIndex]];
        var after = map.Text.Lines[boundaries[endIndex]];
        return Splice(
            map.Source,
            [(before.Start, 0, open + "\n"), (after.Start, 0, close + "\n")]
        );
    }

    static string? SwapLineEndings(SourceMap map, FuzzRandom random) {
        var builder = new StringBuilder(map.Source.Length + 16);
        var mode = random.Next(3);
        var index = 0;
        foreach (var line in map.Text.Lines) {
            builder.Append(map.Source, line.Start, line.End - line.Start);
            if (line.EndIncludingLineBreak > line.End) {
                var crlf = mode switch {
                    0 => true,
                    1 => false,
                    _ => (index & 1) == 0
                };

                builder.Append(crlf ? "\r\n" : "\n");
            }

            index++;
        }

        return builder.ToString();
    }

    static string ToggleBom(SourceMap map) => map.Source.StartsWith('﻿') ? map.Source[1..] : "﻿" + map.Source;

    /// <summary>
    ///     Renames one identifier everywhere it occurs as a token, to a longer name.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the mutation that reaches the fitting engine, and it is the reason the fuzzer is
    ///     worth running at all. docs/plan/16 § R2 argues that the fitter is the only genuinely novel
    ///     code in the project; every decision it makes is a function of a line's *width*, and no other
    ///     mutation in the catalogue changes a width. Widening one name by thirty characters moves a
    ///     call from fitting to not-fitting, which is the boundary M3 found two of four measures
    ///     returning zero at.
    ///     <para>
    ///         Semantic validity is not required and is not attempted — the new name may collide with
    ///         another. What the fuzzer asserts is a formatting property, the formatter is syntactic, and a
    ///         collision produces the same tokens either way.
    ///     </para>
    /// </remarks>
    static string? Widen(SourceMap map, FuzzRandom random) {
        if (map.Identifiers.Count == 0) {
            return null;
        }

        var name = random.Pick(map.Identifiers);
        var occurrences = map.OccurrencesOf(name);
        if (occurrences.Count == 0) {
            return null;
        }

        var widened = name + "_" + new string('w', random.Next(2, 40));
        var edits = occurrences.Select(span => (span.Start, span.Length, widened)).ToList();
        return Splice(map.Source, edits);
    }

    static string? Join(SourceMap map, FuzzRandom random) {
        var joinable = map.Joinable;
        if (joinable.Count == 0) {
            return null;
        }

        var line = random.Pick(joinable);
        var span = map.Text.Lines[line];
        var next = map.Text.Lines[line + 1];
        var indent = 0;
        var content = next.ToString();
        while (indent < content.Length && content[indent] is ' ' or '\t') {
            indent++;
        }

        return Splice(
            map.Source,
            [(span.End, next.Start + indent - span.End, " ")]
        );
    }

    static string? Split(SourceMap map, FuzzRandom random) {
        if (map.Gaps.Count == 0) {
            return null;
        }

        var edits = Sample(map.Gaps, random, 3)
            .Select(gap => (gap.Start, gap.End - gap.Start, "\n"))
            .ToList();

        return edits.Count == 0 ? null : Splice(map.Source, edits);
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>A random non-empty subset, biased small.</summary>
    static List<T> Sample<T>(IReadOnlyList<T> items, FuzzRandom random, int maximum = 24) {
        var count = Math.Min(items.Count, 1 + random.Next(maximum));
        var indices = new List<int>(items.Count);
        for (var i = 0; i < items.Count; i++) {
            indices.Add(i);
        }

        random.Shuffle(indices);
        indices.RemoveRange(count, indices.Count - count);
        indices.Sort();
        return [.. indices.Select(index => items[index])];
    }

    /// <summary>Applies non-overlapping edits, right to left.</summary>
    static string Splice(string source, IReadOnlyList<(int Position, int Delete, string Insert)> edits) {
        var ordered = edits.OrderByDescending(static edit => edit.Position).ToArray();
        var builder = new StringBuilder(source);
        var barrier = source.Length;
        foreach (var edit in ordered) {
            if (edit.Position < 0 || edit.Position + edit.Delete > barrier) {
                continue;
            }

            builder.Remove(edit.Position, edit.Delete);
            builder.Insert(edit.Position, edit.Insert);
            barrier = edit.Position;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Where a mutation is allowed to act, computed once per source.
    /// </summary>
    /// <remarks>
    ///     ⚠ Parsed with the symbols the formatter is about to use, for the reason
    ///     <c>PropertyTests.MutateIndentationOnly</c> records: which lines are disabled text is a
    ///     function of the symbol set, so a map computed from a different set protects the wrong half of
    ///     a <c>#if</c>/<c>#else</c> and the property then fails for the fuzzer's reason rather than the
    ///     formatter's.
    /// </remarks>
    public sealed class SourceMap {
        /// <summary>Lines whose <b>start</b> is inside data — nothing may be inserted before them.</summary>
        readonly HashSet<int> headProtected = [];

        /// <summary>Lines whose <b>end</b> is inside data — nothing may be appended to them.</summary>
        readonly HashSet<int> tailProtected = [];

        /// <summary>
        ///     The extra lines and spans that are data under the <b>other</b> symbol set.
        /// </summary>
        /// <remarks>
        ///     ⚠ The properties are asserted under both symbol sets, so a whitespace-only mutation has
        ///     to be whitespace under both. Which text is <see cref="SyntaxKind.DisabledTextTrivia" /> is
        ///     entirely a function of the symbol set: the <c>#if</c> branch is data with no symbols and
        ///     the <c>#else</c> branch is data with them, and a map built from one set walks straight
        ///     into the other's. It cost this fuzzer 1 639 false absorption reports in a six-minute run
        ///     — one Serilog method with a <c>#if FEATURE_SPAN</c> between its two signatures, found
        ///     over and over — before the two maps were separated.
        ///     <para>
        ///         ⚠ Only the *absorbed* mutations obey this wider protection. A structural mutation may put
        ///         a comment inside a <c>#if</c> body and should: that body is live under one of the two
        ///         sets, and it is the code path M3.1 opened up after the
        ///         <c>&gt;</c>-before-<c>(</c> defect survived four milestones inside it.
        ///     </para>
        /// </remarks>
        readonly HashSet<int> otherHeadProtected = [];

        readonly HashSet<int> otherTailProtected = [];

        readonly List<TextSpan> otherDataRegions = [];

        /// <summary>
        ///     Lines that <b>begin</b> with a comment, hard against the left margin.
        /// </summary>
        /// <remarks>
        ///     ⚠ A correction to the property rather than to the formatter, and the same correction
        ///     <see cref="AbsorbableGaps" /> carries for the gap beside a <c>..</c>:
        ///     <c>resharper_csharp_stick_comment</c> — "Don't indent comments started at first column" —
        ///     makes the comment's <em>source column</em> an input the oracle reads, and "at the first
        ///     column" is literal. Measured on
        ///     <c>constructs/trivia/resharper_csharp_stick_comment.expected.cs</c>: a comment at column 0
        ///     is returned at column 0, and the same comment at column 2 is returned at the code's
        ///     indent. So the one space an <see cref="Indent" /> mutation inserts in front of a column-0
        ///     comment is <b>not</b> whitespace in the sense absorption means — it is the key's entire
        ///     input, and a formatter that absorbed it would be diverging from the oracle on purpose.
        ///     <para>
        ///         ⚠ Head protection only, and only for the absorbed five. Appending to such a line, or
        ///         inserting a whole line above it, leaves the comment where the author put it; it is
        ///         shifting the line's own start that destroys the measurement. A structural mutation may
        ///         still shift it and should — it is allowed to move the output.
        ///     </para>
        ///     <para>
        ///         ⚠ Block comments are in scope too — the oracle applies the key to them — but they arrive
        ///         here already <c>headProtected</c>, because a <see cref="SyntaxKind.MultiLineCommentTrivia" />
        ///         is protected <c>whole</c>. This set is what the <em>single-line</em> kinds were missing.
        ///     </para>
        /// </remarks>
        readonly HashSet<int> commentStartedLines = [];

        readonly HashSet<int> commentEndedLines = [];
        readonly List<TextSpan> verbatimRegions = [];
        readonly Dictionary<string, List<TextSpan>> identifiers = new(StringComparer.Ordinal);

        SourceMap(string source, SourceText text, SyntaxTree tree) {
            Source = source;
            Text = text;
            Tree = tree;
        }

        public string Source { get; }

        public SourceText Text { get; }

        public SyntaxTree Tree { get; }

        /// <summary>Maximal whitespace runs between two tokens on one line, with no trivia between.</summary>
        public IReadOnlyList<TextSpan> Gaps { get; private set; } = [];

        /// <summary>
        ///     The gaps whose width the formatter is obliged to <b>decide</b>, which is a subset.
        /// </summary>
        /// <remarks>
        ///     ⚠ Found by this fuzzer on its first run, and it is a correction to the property rather
        ///     than to the formatter. <c>SpaceRules.Ungoverned</c> answers <c>SpaceKind.Preserve</c> for
        ///     the gap beside a <c>..</c> in a range or a spread, because no key in ReSharper's export
        ///     governs it and the oracle leaves whatever the author wrote there. Asked directly, both
        ///     tools turn <c>a[1..3]</c>, <c>a[1 .. 3]</c> and <c>a[1.. 3]</c> into three different
        ///     outputs — byte-identical to each other, and each preserving its input.
        ///     <para>
        ///         So <c>format(mutate_whitespace(x)) ≡ format(x)</c> is <b>false as stated</b> for that one
        ///         gap class, and asserting it there would be asserting that Skala should diverge from the
        ///         oracle. Excluded by token kind rather than by parent shape: <c>Preserve</c> is produced
        ///         only for a <c>..</c>, so "any gap touching a <c>..</c>" is a conservative superset that
        ///         does not have to track which parent shapes qualify — and if a *new* preserve class ever
        ///         appears, the fuzzer will find it, which is the outcome that wants a decision.
        ///     </para>
        /// </remarks>
        public IReadOnlyList<TextSpan> AbsorbableGaps { get; private set; } = [];

        /// <summary>Line starts a whole line may be inserted before.</summary>
        public IReadOnlyList<int> LineBoundaries { get; private set; } = [];

        /// <summary>Line indices whose break may be replaced by a space.</summary>
        public IReadOnlyList<int> Joinable { get; private set; } = [];

        /// <summary>Maximal <c>(first, last)</c> line runs containing no preprocessor directive.</summary>
        public IReadOnlyList<(int First, int Last)> DirectiveFreeRuns { get; private set; } = [];

        public IReadOnlyList<string> Identifiers { get; private set; } = [];

        public IReadOnlyList<TextSpan> OccurrencesOf(string name) =>
            identifiers.TryGetValue(name, out var spans) ? spans : [];

        public static SourceMap Of(string source, IReadOnlyList<string> symbols) {
            var text = SourceText.From(source);
            var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptionsFor(symbols));
            var map = new SourceMap(source, text, tree);
            map.Build();
            return map;
        }

        /// <summary>
        ///     Lines a mutation may act on.
        /// </summary>
        /// <param name="atStart">
        ///     <c>true</c> for a mutation that rewrites a line's leading whitespace, <c>false</c> for
        ///     one that appends to its end. ⚠ The two are different sets and conflating them is the
        ///     subtle way a fuzzer writes into a raw string: a token that opens on line 5 and closes on
        ///     line 8 leaves line 5's *indentation* real whitespace and line 5's *end* inside the token.
        /// </param>
        public IReadOnlyList<int> SafeLines(bool atStart, bool excludeCommentEnds = false, bool absorbing = false) {
            var lines = new List<int>();
            for (var i = 0; i < Text.Lines.Count; i++) {
                if (atStart ? headProtected.Contains(i) : tailProtected.Contains(i)) {
                    continue;
                }

                if (absorbing && (atStart ? otherHeadProtected.Contains(i) : otherTailProtected.Contains(i))) {
                    continue;
                }

                // ⚠ An absorbed mutation may not touch the *interior* of a verbatim region, and the
                // head/tail protection above does not cover it: `Protect(…, whole: false)` marks the
                // line a region starts on and the line it ends on, which leaves every line between
                // them open. That is correct for a token — nothing is between the two ends of one —
                // and wrong for a multi-line raw string, whose interior lines carry the string's
                // own value. Indenting one changes what the program prints while changing no token
                // the parser reports, so the absorption property fails for the fuzzer's reason
                // rather than the formatter's. Found when a pathological fixture carrying an
                // interpolated raw string with nested braces entered the corpus.
                if (absorbing && TouchesVerbatimRegion(Text.Lines[i].Span)) {
                    continue;
                }

                // ⚠ The comment's own column is data under `resharper_csharp_stick_comment`; see
                // `commentStartedLines`. Shifting the start of a line a comment opens at column 0
                // is the one absorbed edit that changes what the oracle is being asked.
                if (absorbing && atStart && commentStartedLines.Contains(i)) {
                    continue;
                }

                if (excludeCommentEnds && commentEndedLines.Contains(i)) {
                    continue;
                }

                lines.Add(i);
            }

            return lines;
        }

        bool DirectiveLine(int line) => Text.Lines[line].ToString().TrimStart().StartsWith('#');

        void Build() {
            var root = Tree.GetRoot();

            // Lines that carry data rather than whitespace: anything a token or an interpolated
            // string spans across, plus disabled text and multi-line comments in full.
            foreach (var token in root.DescendantTokens(descendIntoTrivia: true)) {
                // ⚠ `whole: true` for a token that spans lines, and that is the rule rather
                // than a list of string kinds. A single-line token has nothing between its two
                // ends, so protecting head and tail protects all of it; a token that spans lines
                // has *interior* lines that are its own value — a multi-line raw string, a
                // verbatim string, an interpolated string with newlines in its holes. Indenting
                // one changes what the program prints while changing no token the parser reports
                // at that position, so the absorption property fails for the fuzzer's reason
                // rather than the formatter's. Found when a pathological fixture carrying a raw
                // interpolated string with nested braces entered the corpus at the M9 merge.
                var spansLines = Text.Lines.GetLineFromPosition(token.SpanStart).LineNumber
                    != Text.Lines.GetLineFromPosition(token.Span.End).LineNumber;
                Protect(token.SpanStart, token.Span.End, spansLines);
            }

            foreach (var node in root.DescendantNodes()) {
                if (node is LiteralExpressionSyntax literal
                    && literal.Token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)) {
                    // ⚠ A multi-line raw string is one *token*, so the token loop above protects
                    // only the line it starts on and the line it ends on — every interior line,
                    // which is where the string's own value lives, was left open.
                    verbatimRegions.Add(node.Span);
                    Protect(node.SpanStart, node.Span.End, false);
                }

                if (node is InterpolatedStringExpressionSyntax) {
                    // ⚠ Verbatim by NodeLayout, so every character inside one is copied
                    // byte-for-byte and no mutation may reach it — not the multi-line ones only.
                    // C# 11 put newlines inside interpolation holes, which is what makes the
                    // *inside* of one reachable from a gap between two ordinary tokens.
                    verbatimRegions.Add(node.Span);
                    Protect(node.SpanStart, node.Span.End, false);
                }
            }

            foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true)) {
                if (trivia.IsKind(SyntaxKind.DisabledTextTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
                    Protect(trivia.SpanStart, trivia.Span.End, true);
                } else if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                           || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)) {
                    // ⚠ Every line the trivia spans, not just the one it ends on. A run of `///`
                    // lines is **one** SingleLineDocumentationCommentTrivia, not one per line, so
                    // marking only the last left every line above it open to a trailing-space
                    // mutation — and the space landed inside an XML text token, which is the
                    // comment's content rather than layout.
                    var first = Text.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
                    var last = Text.Lines.GetLineFromPosition(Math.Max(trivia.SpanStart, trivia.Span.End - 1))
                        .LineNumber;

                    for (var line = first; line <= last; line++) {
                        commentEndedLines.Add(line);

                        // ⚠ A line of this comment that opens hard against the left margin. Inside a
                        // comment run every such line begins with the comment's own `/`, so "no
                        // leading whitespace" is the whole test — and a *trailing* comment
                        // (`M(); // x`) fails it, which is right: it is not at the first column and
                        // the key does not protect it. See `commentStartedLines`.
                        if (Text.Lines[line].ToString().StartsWith('/')) {
                            commentStartedLines.Add(line);
                        }
                    }
                }
            }

            // ⚠ A directive's own line is protected at both ends and for every mutation, not for
            // indentation only: splitting `#if DEBUG` across two lines, joining it with the line
            // below, or appending a `// comment` to `#region x` all produce a different file, and
            // two of the three do not parse.
            for (var i = 0; i < Text.Lines.Count; i++) {
                if (DirectiveLine(i)) {
                    headProtected.Add(i);
                    tailProtected.Add(i);
                }
            }

            BuildOtherSet();
            BuildGaps(root);
            BuildBoundaries();
            BuildIdentifiers(root);
        }

        /// <summary>
        ///     The disabled text of the <em>complementary</em> symbol set.
        /// </summary>
        /// <remarks>
        ///     ⚠ The complement is always the empty set, because the two sets the properties are
        ///     asserted under are "no symbols" and <see cref="Corpus.PropertySymbols" />, and a region
        ///     disabled under a *superset* of symbols is disabled under the empty set too. Parsing
        ///     twice costs one parse per mutation and buys the difference between 1 639 absorption
        ///     reports and the four real findings underneath them.
        /// </remarks>
        void BuildOtherSet() {
            var other = CSharpSyntaxTree.ParseText(Text, CSharpFormatter.ParseOptions).GetRoot();
            foreach (var trivia in other.DescendantTrivia(descendIntoTrivia: true)) {
                if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia)) {
                    continue;
                }

                otherDataRegions.Add(trivia.Span);
                var first = Text.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
                var last = Text.Lines.GetLineFromPosition(Math.Max(trivia.SpanStart, trivia.Span.End - 1)).LineNumber;
                for (var line = first; line <= last; line++) {
                    otherHeadProtected.Add(line);
                    otherTailProtected.Add(line);
                }
            }
        }

        void Protect(int start, int end, bool whole) {
            var first = Text.Lines.GetLineFromPosition(start).LineNumber;
            var last = Text.Lines.GetLineFromPosition(Math.Max(start, end - 1)).LineNumber;
            if (whole) {
                for (var line = first; line <= last; line++) {
                    headProtected.Add(line);
                    tailProtected.Add(line);
                }

                return;
            }

            for (var line = first + 1; line <= last; line++) {
                headProtected.Add(line);
            }

            for (var line = first; line <= last - 1; line++) {
                tailProtected.Add(line);
            }
        }

        void BuildGaps(SyntaxNode root) {
            // ⚠ Adjacent *tokens* with nothing but spaces between them. That is what makes a gap
            // safe by construction rather than by a line-level heuristic: there is no token inside a
            // string, a comment or a run of disabled text, so no gap can land in one. The two
            // exclusions below are the two places where a real token gap is still untouchable — an
            // interpolated string's interior, which the formatter copies verbatim, and a directive
            // line, which has no tokens outside trivia and so is excluded by not descending.
            var gaps = new List<TextSpan>();
            var absorbable = new List<TextSpan>();
            SyntaxToken previous = default;
            foreach (var token in root.DescendantTokens(descendIntoTrivia: false)) {
                if (previous.RawKind != 0 && previous.Span.Length > 0 && token.Span.Length > 0) {
                    var start = previous.Span.End;
                    var end = token.SpanStart;
                    if (end >= start
                        && IsPlainSpaces(Source.AsSpan(start, end - start))
                        && Text.Lines.GetLineFromPosition(start).LineNumber
                        == Text.Lines.GetLineFromPosition(end).LineNumber
                        && !InVerbatimRegion(start)) {
                        var gap = TextSpan.FromBounds(start, end);
                        gaps.Add(gap);
                        if (!previous.IsKind(SyntaxKind.DotDotToken)
                            && !token.IsKind(SyntaxKind.DotDotToken)
                            && !InOtherDisabledText(start)) {
                            absorbable.Add(gap);
                        }
                    }
                }

                if (token.Span.Length > 0) {
                    previous = token;
                }
            }

            Gaps = gaps;
            AbsorbableGaps = absorbable;
        }

        bool InOtherDisabledText(int position) {
            foreach (var region in otherDataRegions) {
                if (position >= region.Start && position <= region.End) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>⚠ Intersection, not containment: a region may start mid-line.</summary>
        bool TouchesVerbatimRegion(TextSpan line) {
            foreach (var region in verbatimRegions) {
                if (line.End > region.Start && line.Start < region.End) {
                    return true;
                }
            }

            return false;
        }

        bool InVerbatimRegion(int position) {
            foreach (var region in verbatimRegions) {
                if (position > region.Start && position < region.End) {
                    return true;
                }
            }

            return false;
        }

        static bool IsPlainSpaces(ReadOnlySpan<char> text) {
            foreach (var character in text) {
                if (character is not (' ' or '\t')) {
                    return false;
                }
            }

            return true;
        }

        void BuildBoundaries() {
            var boundaries = new List<int>();
            var joinable = new List<int>();
            var runs = new List<(int First, int Last)>();
            var runStart = -1;

            for (var i = 0; i < Text.Lines.Count; i++) {
                if (!headProtected.Contains(i)) {
                    boundaries.Add(i);
                }

                // Joining line i with i + 1 is safe when i's end and i + 1's start are both real
                // whitespace and i does not end in a `//` comment — a comment runs to the end of the
                // line and joining would swallow whatever follows it into the comment.
                if (i + 1 < Text.Lines.Count
                    && !tailProtected.Contains(i)
                    && !headProtected.Contains(i + 1)
                    && !commentEndedLines.Contains(i)
                    && Text.Lines[i].ToString().Trim().Length > 0
                    && Text.Lines[i + 1].ToString().Trim().Length > 0) {
                    joinable.Add(i);
                }

                if (DirectiveLine(i)) {
                    if (runStart >= 0 && i - 1 > runStart) {
                        runs.Add((runStart, i - 1));
                    }

                    runStart = -1;
                } else if (runStart < 0) {
                    runStart = i;
                }
            }

            if (runStart >= 0 && Text.Lines.Count - 1 > runStart) {
                runs.Add((runStart, Text.Lines.Count - 1));
            }

            LineBoundaries = boundaries;
            Joinable = joinable;
            DirectiveFreeRuns = runs;
        }

        void BuildIdentifiers(SyntaxNode root) {
            foreach (var token in root.DescendantTokens(descendIntoTrivia: false)) {
                if (!token.IsKind(SyntaxKind.IdentifierToken) || token.Span.Length == 0) {
                    continue;
                }

                var name = token.ValueText;
                if (name.Length == 0 || name.StartsWith('@') || !char.IsLetter(name[0]) && name[0] != '_') {
                    continue;
                }

                // ⚠ A contextual keyword is an IdentifierToken and is not an identifier: its meaning
                // is its spelling. `var` is the one that matters — the generator emits it constantly
                // — and `foreach (var (k, w) in …)` renamed to `foreach (var_wwww (k, w) in …)` is
                // not a wider deconstruction, it is `CS0230: Type and identifier are both required
                // in a foreach statement`. ADR-003 then leaves the file byte-identical and every
                // property holds over it for free, so the case executed, asserted nothing, and was
                // counted. Measured before this line: `widen-identifier` was one of the two
                // mutations behind the run's parse-lost cases.
                if (SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None) {
                    continue;
                }

                if (!identifiers.TryGetValue(name, out var spans)) {
                    identifiers[name] = spans = [];
                }

                spans.Add(token.Span);
            }

            // ⚠ Ordinal order, not dictionary order. A `Dictionary`'s enumeration order is not part
            // of its contract, and a fuzzer whose choice depends on it is a fuzzer whose seed does
            // not reproduce its run.
            Identifiers = [.. identifiers.Keys.Order(StringComparer.Ordinal)];
        }
    }
}
