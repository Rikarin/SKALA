using Rikarin.Skala.Options;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>What one option's default came out as, and how much the evidence is worth.</summary>
/// <param name="Key">The option's canonical spelling.</param>
/// <param name="Value">The value the probe settled on, or null when it did not settle.</param>
/// <param name="Verdict">Why it settled, or why it did not.</param>
public sealed record ProbedDefault(string Key, string? Value, DefaultVerdict Verdict, string Detail);

/// <summary>How much a probed default is worth.</summary>
public enum DefaultVerdict {
    /// <summary>Exactly one value reproduced the defaults run on the option's own fixture.</summary>
    Derived,

    /// <summary>Every value reproduced it: the fixture cannot see this option at all.</summary>
    Insensitive,

    /// <summary>More than one value reproduced it, but not all. Batching cannot tell them apart.</summary>
    Ambiguous,

    /// <summary>No value reproduced it. Something else in the batch moved the fixture.</summary>
    Contradicted,

    /// <summary>The option has no fixture to ask.</summary>
    NoFixture
}

/// <summary>
///     Derives ReSharper's built-in defaults by asking the oracle, because nobody publishes them.
/// </summary>
/// <remarks>
///     ⚠ The problem this solves is not cosmetic. <c>options.json</c>'s <c>default</c> is the
///     <em>export's</em> value, which is Rider's default for most keys and the author's choice for the
///     rest with nothing distinguishing the two (docs/plan/03). So on a configuration that does not
///     carry the export, Skala and Rider fall back differently — measured on Vixen at 45 % of Skala's
///     diff — and <c>skala config distill</c> cannot drop a single key, because dropping one on a
///     guessed default silently changes formatting.
///     <para>
///         The method, and its limits:
///     </para>
///     <list type="number">
///         <item>
///             Run <c>jb cleanupcode</c> over the fixture corpus under a configuration carrying nothing but
///             <c>root = true</c>. That output <em>is</em> ReSharper-with-defaults, by construction.
///         </item>
///         <item>
///             Then run it again, a handful of times, with every option set to its 1st legal value, then its
///             2nd, and so on — batched by value index, because <c>cleanupcode</c>'s startup dominates and one
///             run per option per value is thousands of runs.
///         </item>
///         <item>
///             For each option, compare only <em>its own</em> fixture, the one <c>options.json</c>'s
///             <c>oracle</c> field names. The value whose run reproduces the defaults run on that fixture is
///             the default.
///         </item>
///     </list>
///     <para>
///         ⚠ Options interact, so this is a strong signal and not proof, and the verdicts say which is
///         which. A fixture that matches under every value cannot see its option
///         (<see cref="DefaultVerdict.Insensitive" />); one that matches under several is
///         <see cref="DefaultVerdict.Ambiguous" />; one that matches under none had something else in the
///         batch move it (<see cref="DefaultVerdict.Contradicted" />). Only
///         <see cref="DefaultVerdict.Derived" /> may be written to the registry, and it is written as
///         <c>defaultSource: "oracle-probe"</c> rather than as <c>"resharper-docs"</c>, because it is
///         derived and JetBrains still documents nothing.
///     </para>
/// </remarks>
public static class DefaultsProbe {
    /// <summary>The configuration that produces ReSharper-with-defaults: nothing but the terminator.</summary>
    public const string EmptyConfig = "root = true\n";

    public static IReadOnlyList<ProbedDefault> Run(OracleRunner runner, TextWriter log) {
        var candidates = Candidates();
        var fixtures = FixturesOf(candidates);
        if (fixtures.Count == 0) {
            return [];
        }

        log.WriteLine(
            $"defaults: {candidates.Count.ToString(CultureInfo.InvariantCulture)} options over {fixtures.Count.ToString(CultureInfo.InvariantCulture)} fixtures"
        );

        var baseline = FormatAll(runner, [.. fixtures.Select(static file => (file, EmptyConfig))]);
        log.WriteLine($"  baseline (root = true only): {baseline.Count.ToString(CultureInfo.InvariantCulture)} files");

        var rounds = candidates.Max(static option => option.Values.Count);
        var matched = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var option in candidates) {
            matched[option.Key] = [];
        }

        for (var round = 0; round < rounds; round++) {
            // ⚠ One copy of each fixture per option, in a directory of its own carrying an
            // `.editorconfig` that sets that one key and nothing else. The batching is by value
            // index — every option that has an nth value takes it — and the isolation is by
            // directory, so a fixture is never moved by another option's assignment. A shared
            // configuration was the first attempt and it answered nothing: 197 options, zero
            // fixtures unchanged, because every fixture was perturbed by something else in the batch.
            var work = new List<(CorpusFile File, string Config)>();
            var assigned = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var option in candidates) {
                if (round >= option.Values.Count) {
                    continue;
                }

                assigned[option.Key] = option.Values[round];
                work.Add(
                    (option.Fixture,
                        EmptyConfig + "\n[*.cs]\n" + OracleRunner.OracleKey(option.Key) + " = " + option.Values[round]
                        + "\n")
                );
            }

            var results = FormatAll(runner, work);
            var agreed = 0;
            foreach (var option in candidates) {
                if (!assigned.TryGetValue(option.Key, out var value)) {
                    continue;
                }

                if (!baseline.TryGetValue(option.Fixture.Path, out var expected)
                    || !results.TryGetValue(option.Fixture.Path, out var actual)) {
                    continue;
                }

                if (string.Equals(
                        TextNormalisation.Normalise(expected),
                        TextNormalisation.Normalise(actual),
                        StringComparison.Ordinal
                    )) {
                    matched[option.Key].Add(value);
                    agreed++;
                }
            }

            log.WriteLine(
                $"  round {(round + 1).ToString(CultureInfo.InvariantCulture)}: {work.Count.ToString(CultureInfo.InvariantCulture)} options set, {agreed.ToString(CultureInfo.InvariantCulture)} fixtures unchanged"
            );
        }

        var probed = new List<ProbedDefault>(candidates.Count);
        foreach (var option in candidates) {
            var hits = matched[option.Key];
            probed.Add(
                hits.Count switch {
                    0 => new ProbedDefault(
                        option.Key,
                        null,
                        DefaultVerdict.Contradicted,
                        "no value reproduced the defaults run on " + option.Fixture
                    ),
                    1 => new ProbedDefault(option.Key, hits[0], DefaultVerdict.Derived, option.Fixture.ToString()),
                    _ when hits.Count == option.Values.Count => new ProbedDefault(
                        option.Key,
                        null,
                        DefaultVerdict.Insensitive,
                        option.Fixture + " does not exercise it"
                    ),
                    _ => new ProbedDefault(
                        option.Key,
                        null,
                        DefaultVerdict.Ambiguous,
                        string.Join(", ", hits)
                    )
                }
            );
        }

        return probed;
    }

    /// <summary>
    ///     One <c>cleanupcode</c> run per batch of sixty, because it holds the whole project in memory
    ///     and a corpus-sized one is slow. Same batching as `./build.sh Oracle`.
    /// </summary>
    static Dictionary<string, string> FormatAll(OracleRunner runner, List<(CorpusFile File, string Config)> work) {
        var all = new Dictionary<string, string>(StringComparer.Ordinal);
        const int batch = 60;
        for (var start = 0; start < work.Count; start += batch) {
            var slice = work.Skip(start).Take(batch).ToArray();
            foreach (var (path, body) in runner.FormatIsolated(slice)) {
                all[path] = body;
            }
        }

        return all;
    }

    /// <summary>The options worth asking about: C#-relevant, with a fixture and at least two values.</summary>
    static List<Candidate> Candidates() {
        var candidates = new List<Candidate>();
        foreach (var info in OptionRegistry.All) {
            if (info.Oracle is not { Length: > 0 } glob) {
                continue;
            }

            var file = Corpus.All()
                .FirstOrDefault(f => string.Equals(f.Set + "/" + f.RelativePath, glob, StringComparison.Ordinal));
            if (file is null) {
                continue;
            }

            var values = LegalValues(info).Distinct(StringComparer.Ordinal).ToList();
            if (values.Count < 2) {
                continue;
            }

            candidates.Add(new Candidate(info.Key, values, file));
        }

        return candidates;
    }

    static List<CorpusFile> FixturesOf(List<Candidate> candidates) => [
        .. candidates.Select(static c => c.Fixture).DistinctBy(static f => f.Path, StringComparer.Ordinal)
    ];

    /// <summary>
    ///     The values a probe round may assign.
    /// </summary>
    /// <remarks>
    ///     ⚠ An int has no finite domain, so the probe offers the export's value and the two numbers a
    ///     ReSharper counter is ever likely to hold, each clamped into the option's declared bounds. A
    ///     default outside that set comes back <see cref="DefaultVerdict.Contradicted" /> rather than
    ///     wrong, which is the failure mode to prefer.
    ///     <para>
    ///         ⚠ It shares <see cref="OptionDomain.Probes" /> with the sweep and the coverage tests. It
    ///         used to be a fifth hand-kept copy, and every copy offered <c>0</c> for keys whose floor is
    ///         1 — a probe value the tool now refuses.
    ///     </para>
    /// </remarks>
    static IEnumerable<string> LegalValues(OptionInfo info) => OptionDomain.Probes(info);

    sealed record Candidate(string Key, List<string> Values, CorpusFile Fixture);

    /// <summary>The report `skala config` and docs/plan/03 quote.</summary>
    public static string Render(IReadOnlyList<ProbedDefault> probed) {
        var builder = new StringBuilder();
        foreach (var group in probed.GroupBy(static p => p.Verdict).OrderBy(static g => g.Key)) {
            builder.Append(group.Key.ToString())
                .Append(": ")
                .Append(group.Count().ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        builder.AppendLine();
        foreach (var entry in probed.OrderBy(static p => p.Key, StringComparer.Ordinal)) {
            builder.Append(entry.Key.PadRight(64))
                .Append(entry.Verdict.ToString().PadRight(14))
                .Append((entry.Value ?? "-").PadRight(24))
                .AppendLine(entry.Detail);
        }

        return builder.ToString();
    }
}
