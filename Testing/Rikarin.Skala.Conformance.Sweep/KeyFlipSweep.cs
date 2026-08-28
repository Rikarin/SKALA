using System.Diagnostics;
using System.Globalization;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>
///     A round whose canary fired, recorded so that the committed report carries it.
/// </summary>
/// <remarks>
///     ⚠ Until <c>603fbd3</c> a fired canary was a line on the console and nothing else. The sweep's
///     whole design is that the fast path reads a committed artefact rather than re-running the
///     oracle — so a warning that lives only in the terminal of whoever ran it is a warning the next
///     reader cannot see, and the table they read looks exactly as confident as a healthy one. It has
///     to travel with the numbers it qualifies.
/// </remarks>
/// <param name="Round">The zero-based round, or <see langword="null" /> for the baseline pass.</param>
/// <param name="Population">Options in the round, or fixtures compared at the baseline.</param>
/// <param name="Answered">Of those, how many the oracle returned a body for.</param>
/// <param name="Moved">Of those, how many bodies differed from the input.</param>
/// <param name="Reason">What the count means, in the words the log used.</param>
public sealed record BrokenRound(int? Round, int Population, int Answered, int Moved, string Reason);

/// <summary>What one whole sweep produced, and what it cost.</summary>
public sealed record SweepRun(
    IReadOnlyList<OptionSweep> Options,
    IReadOnlyList<SweepExclusion> Excluded,
    int Rounds,
    int OracleInvocations,
    TimeSpan OracleWallClock,
    TimeSpan SkalaWallClock,
    string OracleVersion,
    string ConfigDigest,
    IReadOnlyList<BrokenRound> BrokenRounds);

/// <summary>
///     Every option, at every legal value, formatted by Skala and by <c>jb cleanupcode</c> under the
///     same configuration, and compared.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why one configuration is not enough.</b> Every differential measurement in this project
///         runs at the values in the Rider export. That measures the output, not the options: an option
///         whose configured value coincides with Skala's behaviour is free, whether Skala implements it or
///         not. This sweep flips each key instead, which is the only measurement that can distinguish
///         "honoured" from "happens to agree".
///     </para>
///     <para>
///         <b>Batching, and the hazard.</b> <c>cleanupcode</c>'s startup dominates — tens of seconds —
///         so one invocation per (option, value) is not viable at ~950 configurations. The sweep batches by
///         value index: one round sets every option to its 1st value, the next to its 2nd, and the round
///         count is the widest option's value count rather than the total. ⚠ M3 hit the hazard in this
///         technique and it is worth restating: with a <em>shared</em> <c>.editorconfig</c> across the batch
///         every fixture is moved by every other option in it, and the first attempt came back "197 options
///         set, 0 fixtures unchanged". Each fixture gets its own directory and its own <c>.editorconfig</c>,
///         so a fixture observes only its own option.
///     </para>
///     <para>
///         <b>The base configuration.</b> Both engines are given the repository's export with exactly
///         one key overridden, so that no key is left to fall back on a default. That matters because Skala
///         and ReSharper fall back <em>differently</em> — the whole reason
///         <c>Rikarin.Skala.Testing.DefaultsProbe</c> exists — and a bare base configuration would make
///         every option's comparison a measurement of the default table rather than of the option. ⚠ The two
///         engines reach the same configuration by different mechanics: the oracle is handed a file that is
///         the export plus an appended <c>[*.cs]</c> section, and Skala is handed the export's chain plus a
///         command-line override, which <see cref="OptionResolver" /> applies last and which therefore wins
///         the same way the appended section does. The baseline pass is the check that the two really are
///         the same configuration: it runs both engines over every fixture with nothing overridden, and a
///         fixture the two disagree on there is reported as such rather than blamed on the flipped key.
///     </para>
///     <para>
///         <b>What is deliberately not swept.</b> One key at a time isolates cleanly, and that is what
///         makes an option's verdict a statement about that option. It is also provably incomplete:
///         docs/plan/05 § <c>keep_existing_*</c> is a four-way table across two keys, and this sweep reaches
///         one line of it. ⚠ <see cref="PairwiseSweep" /> is that second phase and it exists — a green row
///         here is not evidence about any pair, and the two tables are read together.
///     </para>
/// </remarks>
public sealed class KeyFlipSweep {
    /// <summary>
    ///     How many fixture directories go into one <c>cleanupcode</c> invocation.
    /// </summary>
    /// <remarks>
    ///     ⚠ Sixty, the same as <c>./build.sh Oracle</c> and the defaults probe. The tool holds the whole
    ///     project in memory and a corpus-sized one is slow, so the batch trades startup cost against
    ///     working-set cost; sixty is where M1 put it and nothing here has a reason to move it.
    /// </remarks>
    public const int BatchSize = 60;

    readonly OracleRunner _runner;
    readonly string _baseConfig;
    readonly TextWriter _log;

    public KeyFlipSweep(OracleRunner runner, string baseConfigPath, TextWriter log) {
        _runner = runner;
        _baseConfig = File.ReadAllText(baseConfigPath);
        _log = log;
        // ⚠ Through OracleFixture rather than hashed here. The digest this run stamps into
        // conformance-sweep.md is compared against the one the fixture headers carry, and a second
        // implementation of "the digest" is how two files come to disagree about one configuration.
        ConfigDigest = OracleFixture.HashConfig(baseConfigPath);
    }

    public string ConfigDigest { get; }

    /// <summary>
    ///     Whether a count is reporting a broken instrument rather than a dramatic result.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both of this harness's canaries have the same shape — a non-empty population in which
    ///     <em>nothing</em> was observed — and both have fired for real. M3's "197 options set, 0
    ///     fixtures unchanged" was a shared-<c>.editorconfig</c> bug; this harness's own "0/164 fixtures
    ///     agree at the baseline" was a normalise-one-side-only bug. Neither was an error: each produced
    ///     a confident, entirely wrong table, and each was caught only because a human read a count.
    ///     <para>
    ///         ⚠ It is a named predicate rather than two inline comparisons so that it can be pinned by a
    ///         test. A canary that is only exercised when the harness is already broken is a canary nobody
    ///         has checked is alive — and the live sweep cannot demonstrate it, because a healthy run is
    ///         exactly the run in which it stays silent.
    ///     </para>
    /// </remarks>
    /// <param name="population">Fixtures compared, or options in the round.</param>
    /// <param name="observed">Of those, how many agreed, or were answered.</param>
    public static bool IsBrokenMeasurement(int population, int observed) => population > 0 && observed == 0;

    /// <summary>
    ///     Whether a round's configurations never reached the tool: it answered, and answered the same
    ///     thing it was given, for every option in the round.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the M3 signature — "197 options set, 0 fixtures unchanged", a shared
    ///     <c>.editorconfig</c> that meant every configuration was the same configuration — and it is a
    ///     genuinely different question from <see cref="IsBrokenMeasurement" />. There the tool produced
    ///     nothing; here it produced the input back.
    ///     <para>
    ///         ⚠ <b>Suppressed below a population of two, and that is the correction rather than a
    ///         softening.</b> The sweep batches by value index, so a high-arity option runs alone in every
    ///         round past the arity of every other option — <c>csharp_new_line_before_open_brace</c> has
    ///         fifteen values and rounds 5-15 hold nothing else. In a round of one, "no option moved" and
    ///         "this option's value legitimately reproduces its own fixture" are the same observation, and
    ///         the canary cannot tell them apart. It fired on exactly that at <c>603fbd3</c> and the round
    ///         was healthy. The fifteenth value is the flags domain's synthesised all-members join; both
    ///         engines <em>parse</em> it, <c>all</c> is one of its members and dominates the rest, and the
    ///         fixture is already written with every brace on its own line — so the oracle answered, and
    ///         answered with the text it was given. A canary that cries wolf on every run with a
    ///         high-arity option in it is a canary that gets skimmed, which is the failure mode both of
    ///         these exist to avoid.
    ///     </para>
    /// </remarks>
    public static bool IsUnvaryingRound(int population, int moved) => population > 1 && moved == 0;

    public SweepRun Run(SweepPlanResult plan) {
        var candidates = plan.Candidates;
        if (candidates.Count == 0) {
            return new SweepRun(
                [],
                plan.Excluded,
                0,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                _runner.Version,
                ConfigDigest,
                []
            );
        }

        var rounds = candidates.Max(static candidate => candidate.Values.Count);
        _log.WriteLine(
            $"sweep: {Count(candidates.Count)} options, {Count(candidates.Sum(static c => c.Values.Count))} configurations, {Count(rounds)} rounds"
        );

        // ⚠ Keyed on (option, value index) rather than (option, value): two options may legally
        // share a value spelling, and an option may not repeat one.
        var oracle = new Dictionary<(string Key, int Round), string>();
        var skala = new Dictionary<(string Key, int Round), string>();
        var cost = candidates.ToDictionary(static c => c.Key, static _ => TimeSpan.Zero, StringComparer.Ordinal);

        var oracleClock = TimeSpan.Zero;
        var skalaClock = TimeSpan.Zero;
        var invocations = 0;
        var broken = new List<BrokenRound>();

        // ⚠ The base configuration first, with nothing overridden, because a divergence the two
        // engines already have on a fixture is not evidence about the key that was flipped on it.
        var baselineStart = Stopwatch.GetTimestamp();
        var baseline = MeasureBaseline(candidates, ref invocations);
        oracleClock += Stopwatch.GetElapsedTime(baselineStart);
        var agreeing = baseline.Count(static entry => entry.Value);
        _log.WriteLine(
            $"  baseline: {Count(agreeing)}/{Count(baseline.Count)} fixtures already agree under the base configuration"
        );

        // ⚠ Both canaries below are loud rather than logged, because a broken measurement and a
        // dramatic finding look identical in a table. M3's "197 options set, 0 fixtures unchanged"
        // was a shared-configuration bug; this harness's own "0/164 fixtures agree at the baseline"
        // was a normalise-one-side-only bug. Both were caught by a human reading a count. A count
        // that can only be read is a count that will eventually be skimmed.
        //
        // ⚠ And loud is no longer enough: each one is also recorded, because the console it was
        // loud on belongs to whoever ran the sweep and the artefact is what everyone else reads.
        if (IsBrokenMeasurement(baseline.Count, agreeing)) {
            broken.Add(new BrokenRound(
                    null,
                    baseline.Count,
                    baseline.Count,
                    0,
                    "Skala and the oracle disagree on every fixture before any key is flipped. The "
                    + "comparison is broken, so nothing below it can be read."
                )
            );
            _log.WriteLine(
                "  ⚠ NOT A FINDING, A BROKEN MEASUREMENT: Skala and the oracle disagree on every fixture "
                + "before any key is flipped. Check the comparison before reading anything below it."
            );
        }

        for (var round = 0; round < rounds; round++) {
            var work = candidates.Where(candidate => round < candidate.Values.Count).ToArray();
            var moved = 0;

            for (var start = 0; start < work.Length; start += BatchSize) {
                var batch = work.Skip(start).Take(BatchSize).ToArray();

                var skalaStart = Stopwatch.GetTimestamp();
                foreach (var candidate in batch) {
                    skala[(candidate.Key, round)] = FormatWithSkala(candidate, candidate.Values[round]);
                }

                skalaClock += Stopwatch.GetElapsedTime(skalaStart);

                var oracleStart = Stopwatch.GetTimestamp();
                var produced = FormatWithOracle(batch, round);
                var elapsed = Stopwatch.GetElapsedTime(oracleStart);
                oracleClock += elapsed;
                invocations++;

                // The batch's wall clock is one `cleanupcode` startup shared between its members,
                // so the honest per-option figure is the share and not a stopwatch around a call
                // that was never made on its own.
                var share = elapsed / batch.Length;
                for (var i = 0; i < batch.Length; i++) {
                    cost[batch[i].Key] += share;
                    if (produced[i] is { } body) {
                        oracle[(batch[i].Key, round)] = body;
                    }
                }
            }

            // ⚠ `answered` and `moved` are separate counts because the two canaries below ask
            // separate questions, and folding them into one number is what made the canary
            // unreadable at 603fbd3. `answered` is "did `cleanupcode` return a body at all" — a
            // property of the instrument. `moved` is "did the body differ from the input" — a
            // property of the finding. A round in which the tool errored and a round in which one
            // option's value legitimately reproduces its fixture both have `moved == 0`, and only
            // `answered` tells them apart.
            var answered = 0;
            foreach (var candidate in work) {
                if (!oracle.TryGetValue((candidate.Key, round), out var body)) {
                    continue;
                }

                answered++;
                if (!string.Equals(
                        TextNormalisation.Normalise(body),
                        Baseline(candidate),
                        StringComparison.Ordinal
                    )) {
                    moved++;
                }
            }

            _log.WriteLine(
                $"  round {Count(round + 1)}/{Count(rounds)}: {Count(work.Length)} options, {Count(answered)} answered, {Count(moved)} oracle outputs differ from the input"
            );

            if (IsBrokenMeasurement(work.Length, answered)) {
                broken.Add(new BrokenRound(
                        round,
                        work.Length,
                        answered,
                        moved,
                        "`cleanupcode` returned nothing for any option in this round. It errored, or the "
                        + "configuration never reached it."
                    )
                );
                _log.WriteLine(
                    "  ⚠ NOT A FINDING, A BROKEN MEASUREMENT: `cleanupcode` returned nothing for this "
                    + "whole round. It errored, or the configuration never reached it."
                );
            } else if (IsUnvaryingRound(work.Length, moved)) {
                broken.Add(new BrokenRound(
                        round,
                        work.Length,
                        answered,
                        moved,
                        "`cleanupcode` answered every option in this round with the fixture it was given. "
                        + "The configurations are reaching it but they are not varying."
                    )
                );
                _log.WriteLine(
                    "  ⚠ NOT A FINDING, A BROKEN MEASUREMENT: `cleanupcode` answered every option in this "
                    + "round with the input it was given. The configurations are not varying."
                );
            }
        }

        return new SweepRun(
            [
                .. candidates.Select(candidate => Verdict(
                        candidate,
                        oracle,
                        skala,
                        baseline.GetValueOrDefault(candidate.Fixture.Path),
                        cost[candidate.Key]
                    )
                )
            ],
            plan.Excluded,
            rounds,
            invocations,
            oracleClock,
            skalaClock,
            _runner.Version,
            ConfigDigest,
            broken
        );
    }

    static string Baseline(SweepCandidate candidate) =>
        TextNormalisation.Normalise(File.ReadAllText(candidate.Fixture.Path));

    /// <summary>
    ///     Whether Skala and the oracle agree on each distinct fixture under the base configuration.
    /// </summary>
    /// <remarks>
    ///     ⚠ De-duplicated by fixture, not by option: <c>constructs/spaces/</c> has 63 options pointing
    ///     at 63 files but the wrapping and blank-line families share fixtures, and asking the same
    ///     question sixty times is sixty `cleanupcode` slots spent on an answer already held.
    /// </remarks>
    Dictionary<string, bool> MeasureBaseline(IReadOnlyList<SweepCandidate> candidates, ref int invocations) {
        var fixtures = candidates
            .DistinctBy(static candidate => candidate.Fixture.Path, StringComparer.Ordinal)
            .ToArray();
        var agreement = new Dictionary<string, bool>(StringComparer.Ordinal);

        for (var start = 0; start < fixtures.Length; start += BatchSize) {
            var batch = fixtures.Skip(start).Take(BatchSize).ToArray();
            var produced = FormatWithOracle(batch, round: null);
            invocations++;

            for (var i = 0; i < batch.Length; i++) {
                var path = batch[i].Fixture.Path;
                var skala = TextNormalisation.Normalise(
                    CSharpFormatter.Format(path, CSharpFormatter.Read(path), OptionResolver.Resolve(path).Options)
                        .Formatted
                );

                // ⚠ Both sides normalised. `FormatWithOracle` hands back exactly what the tool
                // wrote, because the line-ending and final-newline options need it to; normalising
                // one side and not the other made every one of 164 fixtures disagree at the
                // baseline, which is the shape a comparison bug takes when it looks like a finding.
                agreement[path] = produced[i] is { } oracle
                    && string.Equals(TextNormalisation.Normalise(oracle), skala, StringComparison.Ordinal);
            }
        }

        return agreement;
    }

    static OptionSweep Verdict(
        SweepCandidate candidate,
        Dictionary<(string Key, int Round), string> oracle,
        Dictionary<(string Key, int Round), string> skala,
        bool baselineAgrees,
        TimeSpan cost
    ) {
        var values = new List<SweepValue>(candidate.Values.Count);
        var oracleOutputs = new HashSet<string>(StringComparer.Ordinal);
        var skalaOutputs = new HashSet<string>(StringComparer.Ordinal);
        var oracleRaw = new HashSet<string>(StringComparer.Ordinal);
        var skalaRaw = new HashSet<string>(StringComparer.Ordinal);
        var rawAgreements = 0;

        for (var round = 0; round < candidate.Values.Count; round++) {
            // ⚠ A missing oracle output is a hole in the measurement and never an agreement.
            // `cleanupcode` skips a file it cannot load, and scoring that as "the two agree" is how
            // a harness reports conformance it never observed.
            var hasOracle = oracle.TryGetValue((candidate.Key, round), out var oracleText);
            var hasSkala = skala.TryGetValue((candidate.Key, round), out var skalaText);
            if (hasOracle) {
                oracleRaw.Add(oracleText!);
                oracleOutputs.Add(TextNormalisation.Normalise(oracleText!));
            }

            if (hasSkala) {
                skalaRaw.Add(skalaText!);
                skalaOutputs.Add(TextNormalisation.Normalise(skalaText!));
            }

            if (hasOracle && hasSkala && string.Equals(oracleText, skalaText, StringComparison.Ordinal)) {
                rawAgreements++;
            }

            values.Add(
                new SweepValue(
                    candidate.Values[round],
                    hasOracle ? Digest(oracleText!) : "missing",
                    hasSkala ? Digest(skalaText!) : "missing",
                    hasOracle
                    && hasSkala
                    && string.Equals(
                        TextNormalisation.Normalise(oracleText!),
                        TextNormalisation.Normalise(skalaText!),
                        StringComparison.Ordinal
                    )
                )
            );
        }

        var agreements = values.Count(static value => value.Agree);
        var outcome = OptionSweep.Classify(oracleOutputs.Count, skalaOutputs.Count, agreements, values.Count);

        // ⚠ Fall back to the raw bytes when — and only when — normalisation is what hid the option.
        // `resharper_enforce_line_ending_style` and `resharper_csharp_insert_final_newline` change
        // nothing but the line terminators and the final newline, so the normalised comparison every
        // other measurement in this repository uses reports them Unexercised for a reason that is
        // about the instrument. Both outputs here come from one run on one machine, so raw bytes are
        // trustworthy in a way they are not for a fixture committed from another OS.
        var lineEndingOnly = outcome == SweepOutcome.Unexercised && (oracleRaw.Count > 1 || skalaRaw.Count > 1);
        if (lineEndingOnly) {
            outcome = OptionSweep.Classify(oracleRaw.Count, skalaRaw.Count, rawAgreements, values.Count);
        }

        return new OptionSweep(
            candidate.Key,
            candidate.Info.Tier,
            candidate.Info.Kind,
            candidate.Fixture.ToString(),
            outcome,
            values,
            lineEndingOnly ? oracleRaw.Count : oracleOutputs.Count,
            lineEndingOnly ? skalaRaw.Count : skalaOutputs.Count,
            baselineAgrees,
            lineEndingOnly,
            cost
        );
    }

    /// <summary>
    ///     Skala's answer for one option at one value, resolved from the repository's own chain.
    /// </summary>
    /// <remarks>
    ///     ⚠ Resolved from the fixture's real path and not from a copy in the scratch tree, which is
    ///     both cheaper and safer: <see cref="ConfigurationCache" /> memoises a parsed
    ///     <c>.editorconfig</c> per path with no eviction, and a fresh 294 KB copy per (option, value)
    ///     would fill it with about a thousand parses of the same document.
    /// </remarks>
    public static string FormatWithSkala(SweepCandidate candidate, string value) =>
        SkalaSide.Format(candidate.Fixture.Path, candidate.Key, value);

    /// <summary>
    ///     One <c>cleanupcode</c> invocation over a directory per fixture, each with its own config.
    /// </summary>
    /// <param name="batch">At most <see cref="BatchSize" /> candidates.</param>
    /// <param name="round">
    ///     Which value index to assign, or <see langword="null" /> for the base configuration with
    ///     nothing overridden.
    /// </param>
    string?[] FormatWithOracle(IReadOnlyList<SweepCandidate> batch, int? round) =>
        ScratchTree.Format(
            _runner,
            batch,
            candidate => round is { } index ? ConfigFor(candidate.Key, candidate.Values[index]) : _baseConfig
        );

    /// <summary>
    ///     The export, with one key forced.
    /// </summary>
    /// <remarks>
    ///     ⚠ Appended rather than substituted. An <c>.editorconfig</c>'s last matching assignment of a
    ///     key wins, so appending overrides whatever the export set without having to find it — and the
    ///     export spells the same option three ways in places, which is exactly the search this avoids.
    ///     The appended section is <c>[*.cs]</c>, which is more specific than the export's own
    ///     <c>[*]</c> and than its multi-extension section, and comes last.
    /// </remarks>
    public string ConfigFor(string key, string value) => _baseConfig + "\n[*.cs]\n" + key + " = " + value + "\n";

    static string Digest(string text) => SkalaSide.Digest(text);

    static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
