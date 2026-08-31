using Rikarin.Skala.Testing;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>How much one derived default is worth.</summary>
public enum DefaultsVerdict {
    /// <summary>Exactly one value reproduced the bare run on the option's own fixture.</summary>
    Verified,

    /// <summary>Every value reproduced it: the fixture cannot see this option under bare defaults.</summary>
    Insensitive,

    /// <summary>More than one value reproduced it, but not all.</summary>
    Ambiguous,

    /// <summary>No value reproduced it.</summary>
    Contradicted
}

/// <summary>What the bare pass concluded about one option's ReSharper default.</summary>
/// <param name="Key">The option's canonical spelling.</param>
/// <param name="Value">The value that reproduced the bare run, when exactly one did.</param>
/// <param name="Verdict">Why it settled, or why it did not.</param>
/// <param name="Masked">
///     ⚠ Whether the export-base sweep saw the oracle distinguish this option's values while the bare
///     pass could not. That combination is not "the fixture is too weak": it is a key that ReSharper's
///     own defaults <em>mask</em>, and it is the distinction the M3 probe could not draw.
/// </param>
public sealed record DerivedDefault(string Key, string? Value, DefaultsVerdict Verdict, bool Masked, string Detail);

/// <summary>
///     The defaults measurement, as a by-product of the same machinery.
/// </summary>
/// <remarks>
///     ⚠ Nobody publishes ReSharper's defaults. JetBrains' EditorConfig property tables give each
///     property's name, language and possible values and never its default, so <c>options.json</c>'s
///     <c>default</c> is the <em>export's</em> value — Rider's default for most keys and the author's
///     choice for the rest, with nothing distinguishing the two. That is why <c>skala config distill</c>
///     may drop a key only where <c>defaultSource</c> is verified: dropping one on a guessed default
///     silently changes formatting in whoever's repository accepted the file.
///     <para>
///         The method is M3's and it is unchanged, because it is correct: a <c>jb cleanupcode</c> run under
///         a configuration carrying nothing but <c>root = true</c> <em>is</em> ReSharper-with-defaults by
///         construction, and the value that reproduces it on the option's own fixture is the default.
///     </para>
///     <para>
///         ⚠ What the sweep adds is the cross-check. The M3 probe reported <c>Insensitive</c> for every
///         option whose fixture matched under all values, and could not say whether that meant "the fixture
///         is too weak" or "ReSharper's defaults mask this option". The sweep answers it: an option the
///         export-base run shows the oracle distinguishing is one the fixture <em>can</em> see, so an
///         <c>Insensitive</c> verdict on it is a masking fact about the bare configuration and not a gap in
///         the fixture. Those are marked <see cref="DerivedDefault.Masked" /> and are not evidence that the
///         fixture needs replacing.
///     </para>
/// </remarks>
public sealed class DefaultsPass {
    /// <summary>The configuration that produces ReSharper-with-defaults: nothing but the terminator.</summary>
    public const string BareConfig = "root = true\n";

    readonly OracleRunner runner;
    readonly TextWriter log;

    public DefaultsPass(OracleRunner runner, TextWriter log) {
        this.runner = runner;
        this.log = log;
    }

    public IReadOnlyList<DerivedDefault> Run(SweepPlanResult plan) {
        var candidates = plan.Candidates;
        if (candidates.Count == 0) {
            return [];
        }

        var rounds = candidates.Max(static candidate => candidate.Values.Count);
        log.WriteLine($"defaults: {Count(candidates.Count)} options, {Count(rounds)} rounds, bare base configuration");

        var fixtures = candidates
            .DistinctBy(static candidate => candidate.Fixture.Path, StringComparer.Ordinal)
            .ToArray();
        var bare = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var batch in Batches(fixtures)) {
            var produced = Format(batch, static _ => BareConfig);
            for (var i = 0; i < batch.Count; i++) {
                // Keyed by fixture path, because the bare run is per fixture and several options
                // may share one.
                if (produced[i] is { } body) {
                    bare[batch[i].Fixture.Path] = body;
                }
            }
        }

        log.WriteLine($"  bare baseline: {Count(bare.Count)} fixtures");

        var matched = candidates.ToDictionary(
            static candidate => candidate.Key,
            static _ => new List<string>(),
            StringComparer.Ordinal
        );
        for (var round = 0; round < rounds; round++) {
            var work = candidates.Where(candidate => round < candidate.Values.Count).ToArray();
            var agreed = 0;
            var started = Stopwatch.GetTimestamp();

            foreach (var batch in Batches(work)) {
                var produced = Format(
                    batch,
                    candidate => BareConfig + "\n[*.cs]\n" + candidate.Key + " = "
                        + candidate.Values[round] + "\n"
                );

                for (var i = 0; i < batch.Count; i++) {
                    if (produced[i] is not { } body || !bare.TryGetValue(batch[i].Fixture.Path, out var expected)) {
                        continue;
                    }

                    if (string.Equals(body, expected, StringComparison.Ordinal)) {
                        matched[batch[i].Key].Add(batch[i].Values[round]);
                        agreed++;
                    }
                }
            }

            log.WriteLine(
                $"  round {Count(round + 1)}/{Count(rounds)}: {Count(work.Length)} options set, {Count(agreed)} fixtures unchanged"
                + $" ({Stopwatch.GetElapsedTime(started).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s)"
            );
        }

        return [
            .. candidates.Select(candidate => Verdict(candidate, matched[candidate.Key]))
        ];
    }

    static DerivedDefault Verdict(SweepCandidate candidate, List<string> hits) {
        // ⚠ `Masked` is set from this pass's own evidence rather than from the conformance run's,
        // so that the two passes stay independent: if the bare pass itself saw the oracle produce
        // more than one output while every value still reproduced the baseline, something is wrong
        // with the comparison and not with the fixture. The conformance run's view is folded in by
        // the caller, which has both.
        if (hits.Count == 1) {
            return new(
                candidate.Key,
                hits[0],
                DefaultsVerdict.Verified,
                false,
                candidate.Fixture.ToString()
            );
        }

        if (hits.Count == 0) {
            return new(
                candidate.Key,
                null,
                DefaultsVerdict.Contradicted,
                false,
                "no value reproduced the bare run on " + candidate.Fixture
            );
        }

        if (hits.Count == candidate.Values.Count) {
            // ⚠ `Masked` is false here and only `CrossCheck` may set it. The tempting intra-pass
            // rule — "the bare oracle produced one output" — is vacuously true for every
            // Insensitive verdict, because a value that changed the output cannot also have
            // reproduced the baseline. It made the field read 47 of 47 and mean nothing. Only the
            // export-base run can say whether the fixture could have seen this option.
            return new(
                candidate.Key,
                null,
                DefaultsVerdict.Insensitive,
                false,
                candidate.Fixture + " does not distinguish its values under bare defaults"
            );
        }

        return new(candidate.Key, null, DefaultsVerdict.Ambiguous, false, string.Join(", ", hits));
    }

    /// <summary>
    ///     ⚠ Folds the conformance run's view into the bare pass's verdicts.
    /// </summary>
    /// <remarks>
    ///     An option the export-base sweep watched the oracle distinguish is one whose fixture can see
    ///     it. If the bare pass nevertheless found every value reproducing the baseline, the bare
    ///     defaults are masking it — a fact about the configuration, not a weak fixture — and saying so
    ///     is the difference between "replace this fixture" and "this key is unreachable from bare
    ///     defaults".
    /// </remarks>
    public static IReadOnlyList<DerivedDefault> CrossCheck(
        IReadOnlyList<DerivedDefault> probed,
        IReadOnlyCollection<string> observable
    ) =>
        [
            .. probed.Select(entry => entry.Verdict == DefaultsVerdict.Insensitive && observable.Contains(entry.Key)
                    ? entry with {
                        Masked = true,
                        Detail = entry.Detail + "; the export-base sweep does distinguish it, so bare defaults mask it"
                    }
                    : entry
            )
        ];

    /// <summary>
    ///     ⚠ Partitioned by oracle profile before batching, exactly as the conformance sweep is.
    /// </summary>
    /// <remarks>
    ///     ⚠ This used to cut the list by count alone, which was correct for as long as every candidate
    ///     wanted <c>CSReformatCode</c>. It stopped being correct the moment the plan started offering
    ///     arrangement and doc-comment fixtures: a batch that mixes profiles trips
    ///     <c>ScratchTree.Format</c>'s own guard, and a semantic batch holding one fixture twice
    ///     measures the scratch project. Both rules live in <see cref="ScratchTree.Batches" /> so that
    ///     this pass and the sweep cannot come to differ about them.
    /// </remarks>
    static IEnumerable<IReadOnlyList<SweepCandidate>> Batches(SweepCandidate[] candidates) {
        foreach (var partition in ScratchTree.ByProfile(candidates, static candidate => candidate.Fixture)) {
            foreach (var batch in ScratchTree.Batches(
                         partition.ToArray(),
                         static candidate => candidate.Fixture,
                         partition.Key,
                         KeyFlipSweep.BatchSize
                     )) {
                yield return batch;
            }
        }
    }

    /// <summary>
    ///     One <c>cleanupcode</c> invocation, a directory per fixture.
    /// </summary>
    /// <remarks>
    ///     ⚠ The isolation is what makes this answer a question about one option rather than about a
    ///     configuration. A shared <c>.editorconfig</c> was the first attempt at this in M3 and it
    ///     answered nothing: 197 options, zero fixtures unchanged, because every fixture was perturbed
    ///     by something else in the batch.
    /// </remarks>
    /// <summary>
    ///     ⚠ Normalised, unlike the conformance sweep's. The defaults question is "which value reproduces
    ///     the bare run", which is a question about content; the line-ending and final-newline options
    ///     are the sweep's problem and not this pass's, and comparing raw here would make every fixture's
    ///     terminator part of every option's answer.
    /// </summary>
    string?[] Format(IReadOnlyList<SweepCandidate> batch, Func<SweepCandidate, string> config) => [
        .. ScratchTree.Format(runner, batch, config)
            .Select(static body => body is null ? null : TextNormalisation.Normalise(body))
    ];

    public static string Render(IReadOnlyList<DerivedDefault> probed) {
        var builder = new StringBuilder();
        foreach (var group in probed.GroupBy(static entry => entry.Verdict).OrderBy(static group => group.Key)) {
            builder.Append(group.Key.ToString())
                .Append(": ")
                .Append(Count(group.Count()))
                .AppendLine();
        }

        var masked = probed.Count(static entry => entry.Masked);
        builder.Append("  of which masked by bare defaults: ").Append(Count(masked)).AppendLine();
        builder.AppendLine();

        foreach (var entry in probed.OrderBy(static e => e.Key, StringComparer.Ordinal)) {
            builder.Append(entry.Key.PadRight(58))
                .Append(entry.Verdict.ToString().PadRight(14))
                .Append((entry.Value ?? "-").PadRight(24))
                .AppendLine(entry.Detail);
        }

        return builder.ToString();
    }

    static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
