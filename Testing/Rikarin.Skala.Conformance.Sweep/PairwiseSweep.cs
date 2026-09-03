using Rikarin.Skala.Testing;
using System.Diagnostics;
using System.Globalization;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>What one pairwise run produced, and what it cost.</summary>
public sealed record PairwiseRun(
    IReadOnlyList<PairSweep> Pairs,
    IReadOnlyList<PairExclusion> Excluded,
    int Rounds,
    int OracleInvocations,
    TimeSpan OracleWallClock,
    TimeSpan SkalaWallClock,
    string OracleVersion,
    string ConfigDigest,
    IReadOnlyList<BrokenRound> BrokenRounds);

/// <summary>
///     Two options at once, over the whole grid of their values, against the oracle.
/// </summary>
/// <remarks>
///     <para>
///         <b>The hole this fills.</b> <see cref="KeyFlipSweep" /> flips one key and holds every other at
///         the export's value. Of a two-key grid that visits exactly the row and the column through the
///         export's corner and none of the interior — so a pair of options that are each conformant alone
///         and wrong together comes back green twice, from an instrument working exactly as designed.
///         docs/plan/12 § "Interactions are out of scope" says so in as many words and names this pass as
///         the second phase; docs/plan/05 § <c>keep_existing_*</c> is a
///         <b>
///             four-way table across two
///             keys
///         </b>, three of whose corners no one-at-a-time sweep can reach.
///     </para>
///     <para>
///         ⚠ <b>The verdict that matters is <see cref="PairOutcome.InteractionOnly" />.</b> A disagreement
///         at a corner the single sweep also visits is an ordinary divergence and that sweep already owns
///         it. A disagreement confined to the interior is the finding this pass exists for, and it is
///         separated in the verdict rather than left for a reader to derive — see
///         <see cref="PairCorner.ReachedBySingleSweep" />.
///     </para>
///     <para>
///         ⚠ <b>Everything else is deliberately the single sweep's machinery.</b> The same
///         directory-per-fixture isolation (<see cref="ScratchTree" />), the same base configuration with
///         overrides appended, the same normalisation, the same <see cref="SkalaSide" /> for Skala's half
///         and the same batching by index. A second implementation of any of those is a second thing that
///         can disagree with the first about what was measured, and this repository has already paid for
///         that lesson twice — five copies of the legal-value logic, and two spellings of the config
///         digest.
///     </para>
/// </remarks>
public sealed class PairwiseSweep {
    readonly OracleRunner runner;
    readonly string baseConfig;
    readonly TextWriter log;

    /// <summary>
    ///     What the committed single sweep found each (key, value) doing on its own, or empty.
    /// </summary>
    /// <remarks>
    ///     ⚠ Empty when there is no committed sweep to ask, which excuses nothing — every corner is then
    ///     evidence about the pair, which is the strict reading and the one that cannot hide a finding.
    /// </remarks>
    readonly Dictionary<(string Key, string Value), bool> alone;

    public PairwiseSweep(
        OracleRunner runner,
        string baseConfigPath,
        TextWriter log,
        IReadOnlyDictionary<(string Key, string Value), bool>? measuredAlone = null
    ) {
        this.runner = runner;
        baseConfig = File.ReadAllText(OracleEditorConfig.Reading(baseConfigPath));
        this.log = log;
        alone = measuredAlone is null
            ? []
            : measuredAlone.ToDictionary(static entry => entry.Key, static entry => entry.Value);
        ConfigDigest = OracleFixture.HashConfig(baseConfigPath);
    }

    public string ConfigDigest { get; }

    /// <summary>
    ///     Whether the one-at-a-time sweep visits this corner <b>on this fixture</b>.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         It depends on the secondary alone, and the first version of this predicate got that
    ///         wrong.
    ///     </b> The tempting reading is "reachable when either key is at the export's value",
    ///     because <c>KeyFlipSweep</c> flips one key and leaves the rest at the export's — so the grid's
    ///     whole cross looks covered. It is not, because that sweep measures each key on
    ///     <em>
    ///         that key's
    ///         own
    ///     </em> <c>oracle</c> fixture. The column — primary at the export's value, secondary moved —
    ///     is measured on the <em>secondary's</em> fixture and says nothing about this one.
    ///     <para>
    ///         ⚠ What that error cost, measured: the first run classified 58 disagreeing corners at
    ///         (primary at export, secondary moved) as reachable, and so reported them <c>DIVERGENT</c> —
    ///         filed as duplicates of rows <c>conformance-sweep.md</c> already carries, when in fact
    ///         nothing had ever measured them. The pass reported <b>zero</b> interactions on its first run
    ///         and the number was an artefact of this line.
    ///     </para>
    ///     <para>
    ///         So on a bool × bool grid exactly <em>half</em> the corners are reachable, not three
    ///         quarters: the primary's two values against the secondary's export value.
    ///     </para>
    ///     <para>
    ///         ⚠ The export's value is read from the registry's <c>default</c>, which docs/plan/03 records
    ///         as the export's own value wherever <c>defaultSource</c> is <c>template</c> — and it is
    ///         <c>template</c> or <c>unknown</c> for every entry. A key whose recorded default ever stopped
    ///         matching what the base configuration sets would make this flag wrong, which is why the
    ///         report prints the values it compared rather than only the verdict.
    ///     </para>
    /// </remarks>
    public static bool ReachedBySingleSweep(string? secondaryDefault, string secondary) =>
        string.Equals(secondary, secondaryDefault, StringComparison.Ordinal);

    public PairwiseRun Run(PairwisePlanResult plan) {
        var candidates = plan.Candidates;
        if (candidates.Count == 0) {
            return new PairwiseRun(
                [],
                plan.Excluded,
                0,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                runner.Version,
                ConfigDigest,
                []
            );
        }

        var rounds = candidates.Max(static candidate => candidate.Corners);
        log.WriteLine(
            $"pairwise: {Count(candidates.Count)} pairs, {Count(candidates.Sum(static c => c.Corners))} corners, {Count(rounds)} rounds"
        );

        var oracle = new Dictionary<(string Pair, int Corner), string>();
        var skala = new Dictionary<(string Pair, int Corner), string>();
        var cost = candidates.ToDictionary(static c => Name(c), static _ => TimeSpan.Zero, StringComparer.Ordinal);

        var oracleClock = TimeSpan.Zero;
        var skalaClock = TimeSpan.Zero;
        var invocations = 0;
        var broken = new List<BrokenRound>();

        var baselineStart = Stopwatch.GetTimestamp();
        var baseline = MeasureBaseline(candidates, ref invocations);
        oracleClock += Stopwatch.GetElapsedTime(baselineStart);
        var agreeing = baseline.Count(static entry => entry.Value);
        log.WriteLine(
            $"  baseline: {Count(agreeing)}/{Count(baseline.Count)} fixtures already agree under the base configuration"
        );

        if (KeyFlipSweep.IsBrokenMeasurement(baseline.Count, agreeing)) {
            broken.Add(
                new BrokenRound(
                    null,
                    baseline.Count,
                    baseline.Count,
                    0,
                    "Skala and the oracle disagree on every fixture before any key is set. The comparison "
                    + "is broken, so nothing below it can be read."
                )
            );
            log.WriteLine(
                "  ⚠ NOT A FINDING, A BROKEN MEASUREMENT: Skala and the oracle disagree on every fixture "
                + "before any key is set."
            );
        }

        for (var round = 0; round < rounds; round++) {
            var work = candidates.Where(candidate => round < candidate.Corners).ToArray();
            var moved = 0;
            var answered = 0;

            for (var start = 0; start < work.Length; start += KeyFlipSweep.BatchSize) {
                var batch = work.Skip(start).Take(KeyFlipSweep.BatchSize).ToArray();

                var skalaStart = Stopwatch.GetTimestamp();
                foreach (var candidate in batch) {
                    skala[(Name(candidate), round)] = SkalaSide.Format(
                        candidate.Fixture.Path,
                        Overrides(candidate, round)
                    );
                }

                skalaClock += Stopwatch.GetElapsedTime(skalaStart);

                var oracleStart = Stopwatch.GetTimestamp();
                var produced = ScratchTree.Format(
                    runner,
                    [.. batch.Select(static candidate => candidate.Fixture)],
                    i => ConfigFor(Overrides(batch[i], round))
                );
                var elapsed = Stopwatch.GetElapsedTime(oracleStart);
                oracleClock += elapsed;
                invocations++;

                var share = elapsed / batch.Length;
                for (var i = 0; i < batch.Length; i++) {
                    cost[Name(batch[i])] += share;
                    if (produced[i] is { } body) {
                        oracle[(Name(batch[i]), round)] = body;
                    }
                }
            }

            foreach (var candidate in work) {
                if (!oracle.TryGetValue((Name(candidate), round), out var body)) {
                    continue;
                }

                answered++;
                if (!string.Equals(
                        TextNormalisation.Normalise(body),
                        TextNormalisation.Normalise(File.ReadAllText(candidate.Fixture.Path)),
                        StringComparison.Ordinal
                    )) {
                    moved++;
                }
            }

            log.WriteLine(
                $"  round {Count(round + 1)}/{Count(rounds)}: {Count(work.Length)} pairs, {Count(answered)} answered, {Count(moved)} oracle outputs differ from the input"
            );

            if (KeyFlipSweep.IsBrokenMeasurement(work.Length, answered)) {
                broken.Add(
                    new BrokenRound(
                        round,
                        work.Length,
                        answered,
                        moved,
                        "`cleanupcode` returned nothing for any pair in this round. It errored, or the "
                        + "configuration never reached it."
                    )
                );
                log.WriteLine("  ⚠ NOT A FINDING, A BROKEN MEASUREMENT: `cleanupcode` returned nothing this round.");
            } else if (KeyFlipSweep.IsUnvaryingRound(work.Length, moved)) {
                broken.Add(
                    new BrokenRound(
                        round,
                        work.Length,
                        answered,
                        moved,
                        "`cleanupcode` answered every pair in this round with the fixture it was given. The "
                        + "configurations are reaching it but they are not varying."
                    )
                );
                log.WriteLine(
                    "  ⚠ NOT A FINDING, A BROKEN MEASUREMENT: every pair answered with the input it was given."
                );
            }
        }

        return new PairwiseRun(
            [.. candidates.Select(candidate => Verdict(candidate, oracle, skala, baseline, cost[Name(candidate)]))],
            plan.Excluded,
            rounds,
            invocations,
            oracleClock,
            skalaClock,
            runner.Version,
            ConfigDigest,
            broken
        );
    }

    /// <summary>The export, with both of the pair's keys forced, in order.</summary>
    public string ConfigFor(IReadOnlyList<KeyValuePair<string, string>> overrides) =>
        baseConfig
        + "\n[*.cs]\n"
        + string.Concat(overrides.Select(static o => o.Key + " = " + o.Value + "\n"));

    /// <summary>
    ///     The corner at <paramref name="round" />, as the two assignments that define it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Row-major over (primary, secondary). The order matters only in that it must be the same for
    ///     both engines and stable across runs, because the committed table is reviewed as a diff and a
    ///     re-ordered grid would read as every corner having changed.
    /// </remarks>
    public static IReadOnlyList<KeyValuePair<string, string>> Overrides(PairCandidate candidate, int round) => [
        new(candidate.Primary.Key, candidate.PrimaryValues[round / candidate.SecondaryValues.Count]),
        new(candidate.Secondary.Key, candidate.SecondaryValues[round % candidate.SecondaryValues.Count])
    ];

    /// <summary>
    ///     Whether the single sweep already records one of the pair diverging at this corner's values.
    /// </summary>
    /// <remarks>
    ///     ⚠ A key with no recorded verdict at that value excuses nothing. "Never measured" and "measured
    ///     and disagreed" are opposite states, and treating the first as the second would let an
    ///     unmeasured key excuse every corner it appears in — which is how a pass stops finding anything.
    /// </remarks>
    bool AttributableToOneKey(PairCandidate candidate, IReadOnlyList<KeyValuePair<string, string>> assignments) =>
        (alone.TryGetValue((candidate.Primary.Key, assignments[0].Value), out var primaryAgreed)
            && !primaryAgreed)
        || (alone.TryGetValue((candidate.Secondary.Key, assignments[1].Value), out var secondaryAgreed)
            && !secondaryAgreed);

    PairSweep Verdict(
        PairCandidate candidate,
        Dictionary<(string Pair, int Corner), string> oracle,
        Dictionary<(string Pair, int Corner), string> skala,
        Dictionary<string, bool> baseline,
        TimeSpan cost
    ) {
        var corners = new List<PairCorner>(candidate.Corners);
        var oracleOutputs = new HashSet<string>(StringComparer.Ordinal);
        var skalaOutputs = new HashSet<string>(StringComparer.Ordinal);

        for (var round = 0; round < candidate.Corners; round++) {
            var assignments = Overrides(candidate, round);
            var hasOracle = oracle.TryGetValue((Name(candidate), round), out var oracleText);
            var hasSkala = skala.TryGetValue((Name(candidate), round), out var skalaText);

            if (hasOracle) {
                oracleOutputs.Add(TextNormalisation.Normalise(oracleText!));
            }

            if (hasSkala) {
                skalaOutputs.Add(TextNormalisation.Normalise(skalaText!));
            }

            corners.Add(
                new PairCorner(
                    assignments[0].Value,
                    assignments[1].Value,
                    hasOracle ? SkalaSide.Digest(oracleText!) : "missing",
                    hasSkala ? SkalaSide.Digest(skalaText!) : "missing",

                    // ⚠ A missing output is never an agreement, exactly as in the single sweep.
                    hasOracle
                    && hasSkala
                    && string.Equals(
                        TextNormalisation.Normalise(oracleText!),
                        TextNormalisation.Normalise(skalaText!),
                        StringComparison.Ordinal
                    ),
                    ReachedBySingleSweep(candidate.Secondary.Default, assignments[1].Value),
                    AttributableToOneKey(candidate, assignments)
                )
            );
        }

        return new(
            candidate.Primary.Key,
            candidate.Secondary.Key,
            candidate.Fixture.ToString(),
            PairSweep.Classify(
                oracleOutputs.Count,
                skalaOutputs.Count,
                corners,
                baseline.GetValueOrDefault(candidate.Fixture.Path)
            ),
            corners,
            oracleOutputs.Count,
            skalaOutputs.Count,
            baseline.GetValueOrDefault(candidate.Fixture.Path),
            cost
        );
    }

    /// <summary>
    ///     Both engines on every distinct fixture with nothing overridden.
    /// </summary>
    /// <remarks>
    ///     ⚠ De-duplicated by fixture. The <c>keep</c> family points thirteen pairs at eleven files and
    ///     the <c>wrap</c> family shares fixtures far more heavily than that; asking the same question
    ///     once per pair spends <c>cleanupcode</c> slots on an answer already held.
    /// </remarks>
    Dictionary<string, bool> MeasureBaseline(IReadOnlyList<PairCandidate> candidates, ref int invocations) {
        var fixtures = candidates
            .Select(static candidate => candidate.Fixture)
            .DistinctBy(static fixture => fixture.Path, StringComparer.Ordinal)
            .ToArray();

        var agrees = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var start = 0; start < fixtures.Length; start += KeyFlipSweep.BatchSize) {
            var batch = fixtures.Skip(start).Take(KeyFlipSweep.BatchSize).ToArray();
            var produced = ScratchTree.Format(runner, batch, _ => baseConfig);
            invocations++;

            for (var i = 0; i < batch.Length; i++) {
                agrees[batch[i].Path] = produced[i] is { } body
                    && string.Equals(
                        TextNormalisation.Normalise(body),
                        TextNormalisation.Normalise(SkalaSide.Format(batch[i].Path, [])),
                        StringComparison.Ordinal
                    );
            }
        }

        return agrees;
    }

    static string Name(PairCandidate candidate) => candidate.Primary.Key + " × " + candidate.Secondary.Key;

    static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
