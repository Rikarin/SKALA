using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>What one whole sweep produced, and what it cost.</summary>
public sealed record SweepRun(
    IReadOnlyList<OptionSweep> Options,
    IReadOnlyList<SweepExclusion> Excluded,
    int Rounds,
    int OracleInvocations,
    TimeSpan OracleWallClock,
    TimeSpan SkalaWallClock,
    string OracleVersion,
    string ConfigDigest);

/// <summary>
/// Every option, at every legal value, formatted by Skala and by <c>jb cleanupcode</c> under the
/// same configuration, and compared.
/// </summary>
/// <remarks>
/// <para><b>Why one configuration is not enough.</b> Every differential measurement in this project
/// runs at the values in the Rider export. That measures the output, not the options: an option
/// whose configured value coincides with Skala's behaviour is free, whether Skala implements it or
/// not. This sweep flips each key instead, which is the only measurement that can distinguish
/// "honoured" from "happens to agree".</para>
///
/// <para><b>Batching, and the hazard.</b> <c>cleanupcode</c>'s startup dominates — tens of seconds —
/// so one invocation per (option, value) is not viable at ~950 configurations. The sweep batches by
/// value index: one round sets every option to its 1st value, the next to its 2nd, and the round
/// count is the widest option's value count rather than the total. ⚠ M3 hit the hazard in this
/// technique and it is worth restating: with a <em>shared</em> <c>.editorconfig</c> across the batch
/// every fixture is moved by every other option in it, and the first attempt came back "197 options
/// set, 0 fixtures unchanged". Each fixture gets its own directory and its own <c>.editorconfig</c>,
/// so a fixture observes only its own option.</para>
///
/// <para><b>The base configuration.</b> Both engines are given the repository's export with exactly
/// one key overridden, so that no key is left to fall back on a default. That matters because Skala
/// and ReSharper fall back <em>differently</em> — the whole reason
/// <c>Rikarin.Skala.Testing.DefaultsProbe</c> exists — and a bare base configuration would make
/// every option's comparison a measurement of the default table rather than of the option. ⚠ The two
/// engines reach the same configuration by different mechanics: the oracle is handed a file that is
/// the export plus an appended <c>[*.cs]</c> section, and Skala is handed the export's chain plus a
/// command-line override, which <see cref="OptionResolver"/> applies last and which therefore wins
/// the same way the appended section does. The baseline pass is the check that the two really are
/// the same configuration: it runs both engines over every fixture with nothing overridden, and a
/// fixture the two disagree on there is reported as such rather than blamed on the flipped key.</para>
///
/// <para><b>What is deliberately not swept.</b> One key at a time isolates cleanly, and that is what
/// makes an option's verdict a statement about that option. It is also provably incomplete:
/// docs/plan/05 § <c>keep_existing_*</c> is a four-way table across two keys, and no one-at-a-time
/// sweep can reach three of its corners. Pairwise sweeps of the known-interacting families are a
/// named second phase, not something this pass approximates.</para>
/// </remarks>
public sealed class KeyFlipSweep {
    /// <summary>
    /// How many fixture directories go into one <c>cleanupcode</c> invocation.
    /// </summary>
    /// <remarks>
    /// ⚠ Sixty, the same as <c>./build.sh Oracle</c> and the defaults probe. The tool holds the whole
    /// project in memory and a corpus-sized one is slow, so the batch trades startup cost against
    /// working-set cost; sixty is where M1 put it and nothing here has a reason to move it.
    /// </remarks>
    public const int BatchSize = 60;

    readonly OracleRunner _runner;
    readonly string _baseConfig;
    readonly TextWriter _log;

    public KeyFlipSweep(OracleRunner runner, string baseConfigPath, TextWriter log) {
        _runner = runner;
        _baseConfig = File.ReadAllText(baseConfigPath);
        _log = log;
        ConfigDigest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(_baseConfig)))[..16];
    }

    public string ConfigDigest { get; }

    public SweepRun Run(SweepPlanResult plan) {
        var candidates = plan.Candidates;
        if (candidates.Count == 0) {
            return new SweepRun([], plan.Excluded, 0, 0, TimeSpan.Zero, TimeSpan.Zero, _runner.Version, ConfigDigest);
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
        if (baseline.Count > 0 && agreeing == 0) {
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

            foreach (var candidate in work) {
                if (oracle.TryGetValue((candidate.Key, round), out var body)
                    && !string.Equals(
                        TextNormalisation.Normalise(body),
                        Baseline(candidate),
                        StringComparison.Ordinal
                    )) {
                    moved++;
                }
            }

            _log.WriteLine(
                $"  round {Count(round + 1)}/{Count(rounds)}: {Count(work.Length)} options, {Count(moved)} oracle outputs differ from the input"
            );

            if (work.Length > 0 && moved == 0) {
                _log.WriteLine(
                    "  ⚠ NOT A FINDING, A BROKEN MEASUREMENT: `cleanupcode` changed nothing in this whole "
                    + "round. It errored, or the configuration never reached it."
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
            ConfigDigest
        );
    }

    static string Baseline(SweepCandidate candidate) =>
        TextNormalisation.Normalise(File.ReadAllText(candidate.Fixture.Path));

    /// <summary>
    /// Whether Skala and the oracle agree on each distinct fixture under the base configuration.
    /// </summary>
    /// <remarks>
    /// ⚠ De-duplicated by fixture, not by option: <c>constructs/spaces/</c> has 63 options pointing
    /// at 63 files but the wrapping and blank-line families share fixtures, and asking the same
    /// question sixty times is sixty `cleanupcode` slots spent on an answer already held.
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
    /// Skala's answer for one option at one value, resolved from the repository's own chain.
    /// </summary>
    /// <remarks>
    /// ⚠ Resolved from the fixture's real path and not from a copy in the scratch tree, which is
    /// both cheaper and safer: <see cref="ConfigurationCache"/> memoises a parsed
    /// <c>.editorconfig</c> per path with no eviction, and a fresh 294 KB copy per (option, value)
    /// would fill it with about a thousand parses of the same document.
    /// </remarks>
    public static string FormatWithSkala(SweepCandidate candidate, string value) {
        var resolved = OptionResolver.Resolve(
            candidate.Fixture.Path,
            [new KeyValuePair<string, string>(candidate.Key, value)]
        );

        if (!resolved.ValueErrors.IsEmpty) {
            return "value-error: " + string.Join("; ", resolved.ValueErrors);
        }

        // ⚠ Raw, exactly as the oracle side is. `Verdict` normalises both together; normalising
        // here and not there made `resharper_csharp_insert_final_newline` look INERT — the oracle
        // moving and Skala not — when `skala format --option` on the same fixture writes 12 bytes
        // at `true` and 11 at `false`. The whole point of an option's verdict is that both engines
        // were asked the same question in the same units.
        var text = CSharpFormatter.Read(candidate.Fixture.Path);
        return CSharpFormatter.Format(candidate.Fixture.Path, text, resolved.Options).Formatted;
    }

    /// <summary>
    /// One <c>cleanupcode</c> invocation over a directory per fixture, each with its own config.
    /// </summary>
    /// <param name="batch">At most <see cref="BatchSize"/> candidates.</param>
    /// <param name="round">
    /// Which value index to assign, or <see langword="null"/> for the base configuration with
    /// nothing overridden.
    /// </param>
    string?[] FormatWithOracle(IReadOnlyList<SweepCandidate> batch, int? round) =>
        ScratchTree.Format(
            _runner,
            batch,
            candidate => round is { } index ? ConfigFor(candidate.Key, candidate.Values[index]) : _baseConfig
        );

    /// <summary>
    /// The export, with one key forced.
    /// </summary>
    /// <remarks>
    /// ⚠ Appended rather than substituted. An <c>.editorconfig</c>'s last matching assignment of a
    /// key wins, so appending overrides whatever the export set without having to find it — and the
    /// export spells the same option three ways in places, which is exactly the search this avoids.
    /// The appended section is <c>[*.cs]</c>, which is more specific than the export's own
    /// <c>[*]</c> and than its multi-extension section, and comes last.
    /// </remarks>
    public string ConfigFor(string key, string value) => _baseConfig + "\n[*.cs]\n" + key + " = " + value + "\n";

    static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];

    static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
