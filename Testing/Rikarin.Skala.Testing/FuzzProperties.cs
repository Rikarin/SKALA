using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Formatting;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Options;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Testing;

/// <summary>One property that did not hold, and enough of the input to say why.</summary>
public sealed record PropertyViolation(string Property, bool Defined, string Detail) {
    public override string ToString() => Property + (Defined ? " [symbols]" : " [no symbols]") + ": " + Detail;
}

/// <summary>
///     A deliberate defect injected into the formatter's answer, to measure whether the fuzzer catches it.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/12 § "Fuzzing" does not ask for this and it is here anyway, because "the fuzzer found
///     nothing" is not evidence of anything on its own — a fuzzer whose mutations never reach the
///     formatter also finds nothing, and looks identical from the outside. Each saboteur below breaks
///     exactly one of the properties; <c>fuzz --mutation-test</c> asserts that the property that should
///     notice does, and names the case count it took. A property no saboteur can trip is a property that
///     is not being asserted.
/// </remarks>
public sealed record FormatSaboteur(string Name, string Target, Func<FormatResult, int, FormatResult> Corrupt);

/// <summary>
///     One gap <see cref="SpaceRules.Preserves" /> answers, in token order, with the author's bit.
/// </summary>
/// <param name="Span">The gap between the two tokens.</param>
/// <param name="Line">Its one-based line, for the report.</param>
/// <param name="Spaced">
///     Whether the author wrote any horizontal space — the one bit the formatter carries — or
///     <c>null</c> when the gap is not a plain run of spaces and tabs (it holds a line break or a
///     comment) and so has no bit to preserve.
/// </param>
public readonly record struct UngovernedGap(TextSpan Span, int Line, bool? Spaced);

/// <summary>
///     The seven properties of docs/plan/12 § "Properties", asserted over an arbitrary string.
/// </summary>
/// <remarks>
///     ⚠ The existing suite asserts these over a <see cref="CorpusFile" />, which is a file on disk with
///     an <c>.editorconfig</c> chain above it. A fuzz case is neither — it is a string that existed for
///     four milliseconds — so the assertions had to be lifted off the file system before a fuzzer could
///     reach them. This class is that lift: the checks are the same checks, in the same order, with the
///     same exemptions, and <c>PropertyTests</c> and <c>ArrangementPropertyTests</c> remain the
///     authority on what they mean.
///     <para>
///         ⚠ Token equivalence is re-checked here rather than read off <see cref="FormatOutcome" />, and the
///         difference matters. The formatter compares the streams itself and refuses to emit when they
///         differ, so reading the outcome asks the formatter whether the formatter is correct. Comparing the
///         streams again, from the outside, is an independent assertion — and it is the only form of the
///         property a saboteur can be made to trip, which is how the fuzzer's own oracle gets tested.
///     </para>
///     <para>
///         ⚠ Every property is checked under <b>both</b> symbol sets, which is the default shape of the
///         harness since M3.1. A file wrapped in a <c>#if</c> is disabled text for a formatter with no
///         symbols and is copied byte-for-byte, so every property holds over it trivially and the code that
///         formats its body is never asserted at all — the <c>&gt;</c>-before-<c>(</c> defect survived four
///         milestones in exactly that blind spot.
///     </para>
/// </remarks>
public static class FuzzProperties {
    public const string Idempotency = "idempotency";
    public const string TokenEquivalence = "token-equivalence";
    public const string ParseStability = "parse-stability";
    public const string Determinism = "determinism";
    public const string RangeConsistency = "range-consistency";
    public const string Absorption = "whitespace-absorption";

    /// <summary>
    ///     The property <see cref="Absorption" />'s exclusion stands on. For every gap
    ///     <see cref="SpaceRules.Preserves" /> answers — the range operator's, the spread's, the one between
    ///     a recursive pattern's type and its positional clause — the formatter writes the author's bit
    ///     back, one space if any horizontal space was written and none otherwise, in <em>both</em>
    ///     spellings of the input, and is idempotent over each.
    /// </summary>
    /// <remarks>
    ///     ⚠ Stated rather than left as a hole. The whitespace mutations skip these gaps because the
    ///     oracle keeps what the author wrote in them (#373; docs/plan/12 § "Where the properties are
    ///     not what this document said"), and a skip on its own would let a formatter that absorbed them
    ///     — the old <c>BeforeOpenParen</c> arm did, in one direction — pass every property. So the case
    ///     is formatted as written and again with every such gap flipped, and each output is read back
    ///     gap by gap. The oracle's literal answers for both spellings are pinned by
    ///     <c>constructs/syntax/positional-pattern-after-its-type</c>; this is the same claim over every
    ///     input the fuzzer builds.
    /// </remarks>
    public const string UngovernedGaps = "ungoverned-gap-preservation";

    public const string ArrangementIdempotency = "arrangement-idempotency";
    public const string ArrangementConvergence = "arrangement-convergence";

    /// <summary>⚠ Not a property but the worst outcome: the formatter threw.</summary>
    public const string Crash = "crash";

    /// <summary>⚠ Not a formatter defect. The *mutation* stopped the file parsing; see <see cref="Fuzzer" />.</summary>
    public const string ParseLost = "fuzzer:parse-lost";

    public static readonly ImmutableArray<string> All = [
        Idempotency,
        TokenEquivalence,
        ParseStability,
        Determinism,
        RangeConsistency,
        Absorption,
        UngovernedGaps,
        ArrangementIdempotency,
        ArrangementConvergence,
        Crash
    ];

    /// <summary>
    ///     One deliberate defect per property, for <c>fuzz --mutation-test</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Each of these is a plausible bug rather than an arbitrary corruption, which is what makes
    ///     the measurement worth anything: <see cref="Idempotency" />'s saboteur is an indentation that
    ///     grows by one on every pass — the shape of a real off-by-one in an indent stack;
    ///     <see cref="Absorption" />'s makes the output depend on how much whitespace the *input* had,
    ///     which is precisely the failure mode the preserve-and-repair model of ADR-002 risks.
    /// </remarks>
    public static readonly ImmutableArray<FormatSaboteur> Saboteurs = [
        new FormatSaboteur(
            "indent-drift",
            Idempotency,
            static (result, _) => result with { Formatted = AddOneIndent(result.Formatted) }
        ),
        new FormatSaboteur(
            "token-drop",
            TokenEquivalence,
            static (result, _) => result with { Formatted = DropLast(result.Formatted, ';') }
        ),
        new FormatSaboteur(
            "brace-drop",
            ParseStability,
            static (result, _) => result with { Formatted = DropLast(result.Formatted, '}') }
        ),
        new FormatSaboteur(
            "counter",
            Determinism,
            static (result, call) => result with {
                Formatted = result.Formatted + "// call " + call.ToString(CultureInfo.InvariantCulture) + "\n"
            }
        ),
        new FormatSaboteur(
            "whitespace-echo",
            Absorption,
            static (result, _) => result with {
                Formatted = result.Formatted
                    + "// spaces "
                    + result.Original.ToString().Count(static c => c == ' ').ToString(CultureInfo.InvariantCulture)
                    + "\n"
            }
        ),
        new FormatSaboteur(
            "gap-closer",
            UngovernedGaps,
            static (result, _) => result with { Formatted = CloseUngovernedGaps(result.Formatted) }
        ),
        new FormatSaboteur(
            "edit-merge",
            RangeConsistency,
            static (result, _) => result.Edits.Length < 2
                ? result
                : result with {
                    Edits = [
                        new TextEdit(SourceSpan.FromBounds(0, result.Original.Length), result.Formatted)
                    ]
                }
        )
    ];

    /// <summary>
    ///     Checks every property that applies to <paramref name="source" />.
    /// </summary>
    /// <param name="baseline">
    ///     The formatted text of the *unmutated* input, per symbol set, when the mutation was
    ///     whitespace-only. Supplying it turns on <see cref="Absorption" />; <c>null</c> leaves it off.
    /// </param>
    /// <param name="arrangement">
    ///     Whether to run the arrange-and-format pair as well. ⚠ Off by default and sampled by the
    ///     driver rather than run on every case, because it costs a compilation — tens of times a
    ///     format — and a fuzzer that runs tens of times fewer cases to add one property is usually a
    ///     worse fuzzer. The driver's <c>--arrange-every</c> is where that trade is priced.
    /// </param>
    public static ImmutableArray<PropertyViolation> Check(
        string path,
        string source,
        in FormattingOptions options,
        IReadOnlyList<string> symbols,
        (string None, string Defined)? baseline = null,
        bool arrangement = false,
        FormatSaboteur? saboteur = null,
        CancellationToken cancellation = default
    ) {
        var violations = ImmutableArray.CreateBuilder<PropertyViolation>();
        foreach (var defined in (ReadOnlySpan<bool>)[false, true]) {
            CheckOne(
                violations,
                path,
                source,
                options,
                defined ? symbols : [],
                defined,
                baseline is null ? null : defined ? baseline.Value.Defined : baseline.Value.None,
                arrangement,
                saboteur,
                cancellation
            );
        }

        return violations.ToImmutable();
    }

    /// <summary>
    ///     <c>format(x)</c>, with the saboteur (if any) applied to the answer.
    /// </summary>
    /// <remarks>
    ///     ⚠ The call index is threaded through so that the determinism saboteur has something to be
    ///     non-deterministic *about*. It is the one piece of state in the fuzzer that is not a function
    ///     of the seed, it exists only for <c>--mutation-test</c>, and it is why that mode does not
    ///     report findings into <c>pathological/</c>.
    /// </remarks>
    public static string Format(
        string path,
        string source,
        in FormattingOptions options,
        IReadOnlyList<string> symbols,
        FormatSaboteur? saboteur = null
    ) {
        var result = CSharpFormatter.Format(path, SourceText.From(source), new PhaseOneOptions(options), null, symbols);
        return saboteur is null ? result.Formatted : saboteur.Corrupt(result, 0).Formatted;
    }

    static void CheckOne(
        ImmutableArray<PropertyViolation>.Builder violations,
        string path,
        string source,
        in FormattingOptions options,
        IReadOnlyList<string> symbols,
        bool defined,
        string? baseline,
        bool arrangement,
        FormatSaboteur? saboteur,
        CancellationToken cancellation
    ) {
        var phaseOne = new PhaseOneOptions(options);
        var text = SourceText.From(source);
        var calls = 0;

        FormatResult Run(SourceText input) {
            var result = CSharpFormatter.Format(path, input, phaseOne, null, symbols);
            return saboteur is null ? result : saboteur.Corrupt(result, Interlocked.Increment(ref calls));
        }

        FormatResult first;
        try {
            first = Run(text);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            violations.Add(new PropertyViolation(Crash, defined, exception.GetType().Name + ": " + exception.Message));
            return;
        }

        if (first.Outcome is FormatOutcome.Generated) {
            return;
        }

        if (first.Outcome is FormatOutcome.NotParseable) {
            // ⚠ Reported as a *fuzzer* defect, not a formatter one. ADR-003 leaves an unparseable
            // file byte-identical, so every property below would hold over it for free — a case that
            // asserted nothing while appearing to assert seven things.
            violations.Add(new PropertyViolation(ParseLost, defined, "the mutated input does not parse"));
            return;
        }

        // 2. Token equivalence, compared from the outside rather than read off the outcome.
        if (first.Outcome is FormatOutcome.VerificationFailed) {
            violations.Add(new PropertyViolation(TokenEquivalence, defined, "the formatter refused to emit"));
        } else if (Rikarin.Skala.Formatting.CSharp.TokenEquivalence.Compare(
                       first.Original,
                       SourceText.From(first.Formatted),
                       CSharpFormatter.ParseOptionsFor(symbols),
                       // ⚠ The same allowance the formatter grants itself, and no wider. A
                       // re-wrapped `///` comment changes documentation trivia by design, so
                       // comparing without this reports every reflowed file as a violation — which
                       // is what happened the day the sub-formatter became the default, on a
                       // mutation that had merely re-indented a doc comment. Read off the result
                       // rather than assumed: a file with no doc comment gets the strict comparison.
                       first.ReflowedComments > 0
                   ) is { } failure) {
            violations.Add(
                new PropertyViolation(
                    TokenEquivalence,
                    defined,
                    $"token {failure.Index.ToString(CultureInfo.InvariantCulture)}: "
                    + $"{failure.Before} became {failure.After}"
                )
            );
        }

        // 1. Idempotency. Its verdict is kept for 6b, which asserts idempotency of a second spelling
        //    and must not re-report a defect this check already owns.
        var idempotent = true;
        try {
            var second = Run(SourceText.From(first.Formatted));
            if (!second.Edits.IsEmpty) {
                idempotent = false;
                violations.Add(
                    new PropertyViolation(
                        Idempotency,
                        defined,
                        $"the second pass still wants {second.Edits.Length.ToString(CultureInfo.InvariantCulture)} edit(s): "
                        + string.Join(", ", second.Edits.Take(3))
                    )
                );
            } else if (!string.Equals(second.Formatted, first.Formatted, StringComparison.Ordinal)) {
                idempotent = false;
                violations.Add(
                    new PropertyViolation(Idempotency, defined, FirstDifference(first.Formatted, second.Formatted))
                );
            }
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            idempotent = false;
            violations.Add(
                new PropertyViolation(
                    Crash,
                    defined,
                    "on the second pass: " + exception.GetType().Name + ": " + exception.Message
                )
            );
        }

        // 3. Parse stability.
        if (first.Outcome is FormatOutcome.Formatted) {
            var before = ParseDiagnostics(first.Original, cancellation);
            var after = ParseDiagnostics(SourceText.From(first.Formatted), cancellation);
            if (!before.SequenceEqual(after, StringComparer.Ordinal)) {
                violations.Add(
                    new PropertyViolation(
                        ParseStability,
                        defined,
                        $"[{string.Join(" ", before)}] became [{string.Join(" ", after)}]"
                    )
                );
            }
        }

        // 4. Determinism.
        for (var run = 0; run < 2; run++) {
            if (!string.Equals(Run(text).Formatted, first.Formatted, StringComparison.Ordinal)) {
                violations.Add(
                    new PropertyViolation(
                        Determinism,
                        defined,
                        $"run {(run + 2).ToString(CultureInfo.InvariantCulture)} differed from run 1"
                    )
                );

                break;
            }
        }

        // 5. Range consistency.
        if (!first.Edits.IsEmpty) {
            var half = first.Original.Length / 2;
            var range = SourceSpan.FromBounds(half, first.Original.Length);
            var restricted = EditEmitter.Restrict(first.Edits, range);
            var expected = first.Edits.Count(edit => edit.Span.IntersectsWith(range));
            if (restricted.Count != expected || restricted.Any(edit => !first.Edits.Contains(edit))) {
                violations.Add(
                    new PropertyViolation(
                        RangeConsistency,
                        defined,
                        $"a range of [{half.ToString(CultureInfo.InvariantCulture)}, end) restricted to "
                        + $"{restricted.Count.ToString(CultureInfo.InvariantCulture)} edit(s) where "
                        + $"{expected.ToString(CultureInfo.InvariantCulture)} intersect"
                    )
                );
            }

            for (var i = 1; i < first.Edits.Length; i++) {
                if (first.Edits[i - 1].Span.End > first.Edits[i].Span.Start) {
                    violations.Add(
                        new PropertyViolation(
                            RangeConsistency,
                            defined,
                            $"edits {(i - 1).ToString(CultureInfo.InvariantCulture)} and "
                            + $"{i.ToString(CultureInfo.InvariantCulture)} overlap or are out of order"
                        )
                    );

                    break;
                }
            }

            // ⚠ Every edit spans the smallest range that differs — no shared first character, no
            // shared last character with the text it replaces.
            //
            // This assertion exists because of `fuzz --mutation-test`. The three checks above are
            // all satisfied by an edit list that has been collapsed into one whole-file edit: it
            // intersects the range, so the count matches; it is in the list, so containment holds;
            // there is one of it, so nothing overlaps. Range formatting would then silently be
            // whole-file formatting and every property still passed. The `edit-merge` saboteur
            // survived 400 cases against the earlier version of this property, which is exactly what
            // a saboteur is for: a property nothing can trip is a property that is not asserted.
            var original = first.Original.ToString();
            for (var i = 0; i < first.Edits.Length; i++) {
                var edit = first.Edits[i];
                if (edit.Span.End > original.Length) {
                    violations.Add(
                        new PropertyViolation(
                            RangeConsistency,
                            defined,
                            $"edit {i.ToString(CultureInfo.InvariantCulture)} runs past the end of the input"
                        )
                    );

                    break;
                }

                var replaced = original.Substring(edit.Span.Start, edit.Span.End - edit.Span.Start);
                if (replaced.Length == 0 || edit.NewText.Length == 0) {
                    continue;
                }

                if (replaced[0] == edit.NewText[0] || replaced[^1] == edit.NewText[^1]) {
                    violations.Add(
                        new PropertyViolation(
                            RangeConsistency,
                            defined,
                            $"edit {i.ToString(CultureInfo.InvariantCulture)} of "
                            + $"{first.Edits.Length.ToString(CultureInfo.InvariantCulture)} is not trimmed to what "
                            + $"differs: it replaces {Quote(replaced)} with {Quote(edit.NewText)}"
                        )
                    );

                    break;
                }
            }

            // The list, applied, must reproduce the output. Independent of the writer that produced
            // both, which is the only form of this check worth having.
            if (!string.Equals(EditEmitter.Apply(original, first.Edits), first.Formatted, StringComparison.Ordinal)) {
                violations.Add(
                    new PropertyViolation(
                        RangeConsistency,
                        defined,
                        "applying the edit list to the input does not reproduce the formatted output"
                    )
                );
            }
        }

        // 6. Whitespace absorption — `format(mutate_whitespace(x)) ≡ format(x)`.
        //
        // ⚠ A file whose braces straddle a `#if` opts out, and the exemption is
        // `PropertyTests.WhitespaceMutation_IsAbsorbed`'s own: such a member is copied byte-for-byte
        // (SK9098, FormatDiagnosticIds.UnbalancedPreprocessor), so whitespace inside it is not
        // whitespace, it is data, and absorbing it would be *losing* it. Measured before this
        // exemption was carried across: 1 866 of the run's 1 952 absorption violations were this
        // one file shape — a Serilog method with a `#if FEATURE_SPAN` between its two signatures.
        if (baseline is not null
            && !string.Equals(baseline, first.Formatted, StringComparison.Ordinal)
            && !first.Diagnostics.Any(static d => d.Id == FormatDiagnosticIds.UnbalancedPreprocessor)) {
            violations.Add(new PropertyViolation(Absorption, defined, FirstDifference(baseline, first.Formatted)));
        }

        // 6b. Ungoverned gaps — the property the absorption check's exclusion stands on. The gaps
        //     `SpaceRules.Preserves` answers are the gaps the whitespace mutations may not touch, so
        //     the formatter's behaviour over them is asserted here instead: the author's bit comes
        //     back in the output, in the spelling the case has and in the flipped one, and the
        //     flipped spelling is idempotent.
        CheckUngovernedGaps(violations, first, Run, symbols, defined, idempotent);

        if (!arrangement) {
            return;
        }

        // 7. The arrange-and-format pair, M4's need #3: `pipeline(pipeline(x)) == pipeline(x)`,
        //    within the bound of four passes.
        PipelineResult pipeline;
        try {
            pipeline = RunPipeline(path, text, options, symbols, CompileOne(path, text, symbols));
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            violations.Add(
                new PropertyViolation(
                    Crash,
                    defined,
                    "in the arrangement pipeline: " + exception.GetType().Name + ": " + exception.Message
                )
            );

            return;
        }

        if (!pipeline.Converged) {
            violations.Add(
                new PropertyViolation(
                    ArrangementConvergence,
                    defined,
                    "arrange-and-format did not reach a fixed point in "
                    + ArrangementPipeline.MaxPasses.ToString(CultureInfo.InvariantCulture)
                    + " passes"
                )
            );

            return;
        }

        var rewritten = SourceText.From(pipeline.Text);
        var again = RunPipeline(path, rewritten, options, symbols, CompileOne(path, rewritten, symbols));
        if (!again.Edits.IsEmpty) {
            violations.Add(
                new PropertyViolation(
                    ArrangementIdempotency,
                    defined,
                    $"the second pipeline pass still wants {again.Edits.Length.ToString(CultureInfo.InvariantCulture)} "
                    + $"edit(s); rules applied on the first: {string.Join(", ", pipeline.Applied)}"
                )
            );
        }
    }

    static void CheckUngovernedGaps(
        ImmutableArray<PropertyViolation>.Builder violations,
        FormatResult first,
        Func<SourceText, FormatResult> run,
        IReadOnlyList<string> symbols,
        bool defined,
        bool idempotent
    ) {
        if (first.Outcome is not FormatOutcome.Formatted) {
            return;
        }

        var source = first.Original.ToString();
        var gaps = UngovernedGapsOf(source, symbols);
        if (gaps.IsEmpty) {
            return;
        }

        if (ReadBack(source, gaps, first.Formatted, symbols) is { } asWritten) {
            violations.Add(new PropertyViolation(UngovernedGaps, defined, "as written, " + asWritten));
        }

        // ⚠ Every flippable gap at once, not one format per gap: a corpus file can hold dozens of
        // ranges, and the claim is per gap either way because the output is read back gap by gap.
        var flipped = FlipUngovernedGaps(source, gaps);
        if (flipped is null) {
            return;
        }

        // ⚠ `Preserves` is false wherever `MustSeparate` is true, so closing a preserved gap cannot
        // re-lex the file. If it ever did, the closed spelling would not be an input that exists and
        // there would be nothing to assert; token equivalence on the case itself is where a
        // `MustSeparate` hole is reported.
        if (Rikarin.Skala.Formatting.CSharp.TokenEquivalence.Compare(
                first.Original,
                SourceText.From(flipped),
                CSharpFormatter.ParseOptionsFor(symbols)
            ) is not null) {
            return;
        }

        FormatResult second;
        try {
            second = run(SourceText.From(flipped));
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            violations.Add(
                new PropertyViolation(
                    Crash,
                    defined,
                    "on the flipped spelling of its ungoverned gaps: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                )
            );

            return;
        }

        if (second.Outcome is not FormatOutcome.Formatted) {
            violations.Add(
                new PropertyViolation(
                    UngovernedGaps,
                    defined,
                    "the flipped spelling of its ungoverned gaps came back " + second.Outcome
                )
            );

            return;
        }

        if (ReadBack(flipped, UngovernedGapsOf(flipped, symbols), second.Formatted, symbols) is { } asFlipped) {
            violations.Add(new PropertyViolation(UngovernedGaps, defined, "flipped, " + asFlipped));
        }

        // ⚠ Only when the spelling the case has is itself a fixed point. A file that is not idempotent
        // as written is not idempotent flipped either, for the same reason, and `idempotency` has
        // already reported it — seed 5209185227727739433 (#375) said so twice under two names until
        // this guard. A red here therefore always means the flip is what broke it.
        if (!idempotent) {
            return;
        }

        var third = run(SourceText.From(second.Formatted));
        if (!third.Edits.IsEmpty || !string.Equals(third.Formatted, second.Formatted, StringComparison.Ordinal)) {
            violations.Add(
                new PropertyViolation(
                    UngovernedGaps,
                    defined,
                    "the flipped spelling is not idempotent: " + FirstDifference(second.Formatted, third.Formatted)
                )
            );
        }
    }

    /// <summary>
    ///     The gaps <see cref="SpaceRules.Preserves" /> answers in <paramref name="source" />, in token
    ///     order, each with the bit the author wrote.
    /// </summary>
    /// <remarks>
    ///     ⚠ Asked of <c>SpaceRules</c> and not of a list kept here, for the reason
    ///     <see cref="FuzzMutations.SourceMap.AbsorbableGaps" /> records: a list drifts the day the
    ///     formatter learns a new ungoverned gap, and this property has to describe that gap from that
    ///     day. Zero-width tokens are skipped as <see cref="FuzzMutations.SourceMap" /> skips them; the
    ///     end-of-file token has no gap in front of it worth a bit.
    /// </remarks>
    public static ImmutableArray<UngovernedGap> UngovernedGapsOf(string source, IReadOnlyList<string> symbols) {
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptionsFor(symbols));
        var gaps = ImmutableArray.CreateBuilder<UngovernedGap>();
        SyntaxToken previous = default;
        foreach (var token in tree.GetRoot().DescendantTokens(descendIntoTrivia: false)) {
            if (token.Span.Length == 0) {
                continue;
            }

            if (previous.RawKind != 0 && SpaceRules.Preserves(previous, token)) {
                var span = TextSpan.FromBounds(previous.Span.End, token.SpanStart);
                var between = source.AsSpan(span.Start, span.Length);
                bool? spaced = null;
                if (IsHorizontal(between)) {
                    spaced = between.Length > 0;
                }

                gaps.Add(new UngovernedGap(span, text.Lines.GetLineFromPosition(span.Start).LineNumber + 1, spaced));
            }

            previous = token;
        }

        return gaps.ToImmutable();
    }

    /// <summary>
    ///     <paramref name="source" /> with every gap that carries a bit flipped — the space taken out
    ///     of a spaced gap, one space put into a closed one — or <c>null</c> if no gap carries a bit.
    /// </summary>
    public static string? FlipUngovernedGaps(string source, ImmutableArray<UngovernedGap> gaps) {
        var flippable = gaps.Where(static gap => gap.Spaced is not null).ToList();
        if (flippable.Count == 0) {
            return null;
        }

        var builder = new System.Text.StringBuilder(source.Length + flippable.Count);
        var position = 0;
        foreach (var gap in flippable) {
            builder.Append(source, position, gap.Span.Start - position);
            builder.Append(gap.Spaced is true ? "" : " ");
            position = gap.Span.End;
        }

        builder.Append(source, position, source.Length - position);
        return builder.ToString();
    }

    /// <summary>
    ///     The first ungoverned gap whose bit the output does not carry, described, or <c>null</c>
    ///     when every bit came back.
    /// </summary>
    /// <remarks>
    ///     Gap <c>i</c> of the input is gap <c>i</c> of the output because the token streams are the
    ///     same — that is <see cref="TokenEquivalence" />'s claim, and when the counts differ it is that
    ///     property's violation, not this one's, so the comparison stands down. A gap the output broke
    ///     across a line, or the input wrote with a comment in it, has no bit and is skipped on that side.
    /// </remarks>
    static string? ReadBack(
        string input,
        ImmutableArray<UngovernedGap> inputGaps,
        string output,
        IReadOnlyList<string> symbols
    ) {
        var outputGaps = UngovernedGapsOf(output, symbols);
        if (outputGaps.Length != inputGaps.Length) {
            return null;
        }

        for (var i = 0; i < inputGaps.Length; i++) {
            if (inputGaps[i].Spaced is not { } wrote || outputGaps[i].Spaced is not { } got || wrote == got) {
                continue;
            }

            return $"ungoverned gap {(i + 1).ToString(CultureInfo.InvariantCulture)} of "
                + $"{inputGaps.Length.ToString(CultureInfo.InvariantCulture)}, "
                + $"line {inputGaps[i].Line.ToString(CultureInfo.InvariantCulture)}: the author wrote "
                + Quote(LineOf(input, inputGaps[i].Span.Start))
                + " and the formatter wrote "
                + Quote(LineOf(output, outputGaps[i].Span.Start));
        }

        return null;
    }

    /// <summary>
    ///     The <c>gap-closer</c> saboteur: a rule that answers every ungoverned gap with "closed", which is
    ///     the shape of the defect #373 removed — <c>BeforeOpenParen</c> answered the pattern gap with a
    ///     space in every position, whatever the author wrote.
    /// </summary>
    static string CloseUngovernedGaps(string formatted) {
        var gaps = UngovernedGapsOf(formatted, []);
        var closed = new System.Text.StringBuilder(formatted.Length);
        var position = 0;
        foreach (var gap in gaps) {
            if (gap.Spaced is not true) {
                continue;
            }

            closed.Append(formatted, position, gap.Span.Start - position);
            position = gap.Span.End;
        }

        closed.Append(formatted, position, formatted.Length - position);
        return closed.ToString();
    }

    static bool IsHorizontal(ReadOnlySpan<char> text) {
        foreach (var character in text) {
            if (character is not (' ' or '\t')) {
                return false;
            }
        }

        return true;
    }

    static string LineOf(string text, int position) {
        var lines = SourceText.From(text).Lines;
        return lines[lines.GetLineFromPosition(position).LineNumber].ToString().Trim();
    }

    static PipelineResult RunPipeline(
        string path,
        SourceText text,
        in FormattingOptions options,
        IReadOnlyList<string> symbols,
        CSharpCompilation compilation
    ) =>
        ArrangementPipeline.Run(
            path,
            text,
            new PhaseOneOptions(options),
            new ArrangementOptions(options),
            compilation,
            ArrangementDifferential.Removable(compilation, path),
            null,
            null,
            symbols,
            ArrangementFilter.All
        );

    /// <summary>
    ///     A one-file loose compilation, with the same implicit usings the corpus compilation carries.
    /// </summary>
    /// <remarks>
    ///     ⚠ One file, unlike <see cref="ArrangementDifferential.Compile" />, and the difference is not
    ///     an oversight. A fuzz case *is* one file: there is no project around a generated compilation
    ///     unit, and a mutated corpus file no longer matches the one its neighbours were compiled
    ///     against. It makes the unused-using question narrower than it is in the product — every
    ///     cross-file using looks removable — which is why the arrangement half of the fuzzer asserts
    ///     idempotency and convergence and deliberately does not assert *what* was arranged.
    /// </remarks>
    static CSharpCompilation CompileOne(string path, SourceText text, IReadOnlyList<string> symbols) {
        var parse = CSharpFormatter.ParseOptionsFor(symbols);
        return CSharpCompilation.Create(
            "fuzz",
            [
                CSharpSyntaxTree.ParseText(
                    SourceText.From(ArrangementDifferential.ImplicitUsings),
                    parse,
                    "GlobalUsings.g.cs"
                ),
                CSharpSyntaxTree.ParseText(text, parse, path)
            ],
            SharedFrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }

    static string[] ParseDiagnostics(SourceText text, CancellationToken cancellation) => [
        .. CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, string.Empty, cancellation)
            .GetDiagnostics(cancellation)
            .Select(static diagnostic => diagnostic.Id)
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>The first line the two outputs disagree on, which is what a report has room for.</summary>
    public static string FirstDifference(string expected, string actual) {
        var left = expected.ReplaceLineEndings("\n").Split('\n');
        var right = actual.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++) {
            var a = i < left.Length ? left[i] : "<end of file>";
            var b = i < right.Length ? right[i] : "<end of file>";
            if (!string.Equals(a, b, StringComparison.Ordinal)) {
                return $"line {(i + 1).ToString(CultureInfo.InvariantCulture)}: expected {Quote(a)} got {Quote(b)}";
            }
        }

        return "the two differ only in line endings";
    }

    static string Quote(string line) => "«" + (line.Length > 90 ? line[..90] + "…" : line) + "»";

    static string AddOneIndent(string formatted) {
        var lines = formatted.Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            if (lines[i].StartsWith(' ')) {
                lines[i] = " " + lines[i];
                return string.Join("\n", lines);
            }
        }

        return formatted;
    }

    static string DropLast(string formatted, char character) {
        var index = formatted.LastIndexOf(character);
        return index < 0 ? formatted : formatted.Remove(index, 1);
    }
}
