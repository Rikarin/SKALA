using Rikarin.Skala.Testing;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>
///     Materialises the committed key-flip sweep's per-configuration outputs into
///     <c>Testing/corpus/sweep/</c>.
/// </summary>
/// <remarks>
///     ⚠ <b>This is a materialisation and not a measurement, and that distinction is the whole gate.</b>
///     <c>build/Build.cs</c>'s <c>Oracle</c> target records the hazard this has to avoid — "an oracle
///     that updates itself when it disagrees is a tautology" — and it applies here with more force,
///     because the corpus this writes is what the repository will have <em>after</em> there is no second
///     opinion left to appeal to. So this pass is not allowed to decide what the oracle produced. Every
///     byte it writes has to hash to an <c>OracleHash</c> that <c>conformance-sweep.json</c> — a file
///     committed in an earlier reviewed commit, by a run that had <c>jb cleanupcode</c> in front of it —
///     already records. An output whose digest does not match is refused, not written.
///     <para>
///         ⚠ <b>What that buys, concretely.</b> 676 of the 682 distinct outputs are ones Skala reproduces
///         byte for byte, so their bytes are taken from Skala — and the reason that is not circular is
///         the hash: Skala is being used as a decompressor for a digest the oracle certified, and a Skala
///         that had regressed would produce a different digest and be refused. The remaining 6 are on
///         rows where Skala is known to disagree, so no digest of Skala's can match and only
///         <c>jb cleanupcode</c> can supply them.
///     </para>
///     <para>
///         ⚠ <b>And a second, blunter gate before any of that.</b> If Skala's output at any configuration
///         no longer matches the <c>SkalaHash</c> the sweep recorded, the freeze refuses outright and
///         writes nothing. The sweep's row is then a statement about a formatter that no longer exists —
///         <c>ProvenanceTests.TheCommittedSweep_MeasuredTheFormatterInForce</c> is already red at that
///         point — and freezing a corpus off it would launder a drifted formatter into a permanent
///         standard. The fix is <c>./build.sh Sweep</c>, against the real oracle, in its own commit.
///     </para>
///     <para>
///         ⚠ <b>All or nothing.</b> The manifest is written only when every distinct output was obtained
///         and verified. A half-frozen corpus is the state with no honest reading: the replay test cannot
///         tell a configuration nobody froze from one that was deliberately left out.
///     </para>
/// </remarks>
public static class FrozenFreeze {
    /// <summary>How many refusals to name before falling back to a count.</summary>
    const int Examples = 10;

    public static int Run(string sweepDirectory, TextWriter log) {
        var json = Path.Combine(sweepDirectory, "conformance-sweep.json");
        var markdown = Path.Combine(sweepDirectory, "conformance-sweep.md");

        if (!File.Exists(json) || !File.Exists(markdown)) {
            log.WriteLine(
                "there is no committed sweep to freeze. `./build.sh Sweep` writes both halves of the pair, "
                + "and this pass reads them — it does not measure anything itself."
            );

            return 2;
        }

        var report = File.ReadAllText(markdown);
        if (Recorded(report, @"base configuration \|[^|]*sha256 `(?<value>[0-9a-f]+)`") is not { } digest) {
            log.WriteLine(markdown + " records no base-configuration digest. Re-run `./build.sh Sweep`.");
            return 2;
        }

        if (Recorded(report, @"\| ReSharper \| (?<value>[^|]+?) \|") is not { } version) {
            log.WriteLine(markdown + " records no ReSharper version. Re-run `./build.sh Sweep`.");
            return 2;
        }

        // ⚠ The provenance gate, and it comes first because everything after it would otherwise be a
        // measurement of a configuration nobody is using. `ProvenanceTests` makes exactly this check
        // for the corpus and for the sweep; a freeze that skipped it would commit a third artefact
        // that silently disagrees with both.
        var inForce = OracleFixture.ConfigDigestInForce();
        if (!string.Equals(digest, inForce, StringComparison.Ordinal)) {
            log.WriteLine(
                "the committed sweep was measured against a configuration that is no longer in force, so "
                + "freezing its outputs would freeze somebody else's configuration.\n"
                + "  "
                + Corpus.OracleEditorConfigPath
                + "\n  on disk now:       sha256:"
                + inForce
                + "\n  the sweep records: sha256:"
                + digest
                + "\nRe-run `./build.sh Sweep` first."
            );

            return 2;
        }

        var rows = JsonSerializer.Deserialize<SweepRow[]>(File.ReadAllText(json));
        if (rows is not { Length: > 0 }) {
            log.WriteLine(json + " holds no rows. That is a broken archive, not a clean sweep.");
            return 2;
        }

        var divergences = Divergences.Read(Path.Combine(Corpus.RepositoryRoot, "docs", "divergences.md"));
        var fixtures = Corpus.All().ToDictionary(static file => file.ToString(), StringComparer.Ordinal);

        var plan = Plan(rows, fixtures, divergences, log);
        if (plan is null) {
            return 1;
        }

        return Materialise(plan, fixtures, version, digest, log);
    }

    /// <summary>
    ///     Every configuration the sweep measured, with the frozen output it will point at — or
    ///     <see langword="null" /> when the sweep is not in a state that can be frozen at all.
    /// </summary>
    static List<FrozenConfiguration>? Plan(
        IReadOnlyList<SweepRow> rows,
        Dictionary<string, CorpusFile> fixtures,
        IReadOnlyDictionary<string, string> divergences,
        TextWriter log
    ) {
        var configurations = new List<FrozenConfiguration>();
        var unrecorded = new List<string>();
        var unknownFixture = new List<string>();
        var unargued = new List<string>();

        foreach (var row in rows) {
            if (row.Fixture is not { Length: > 0 } fixture || row.Values is not { Count: > 0 } values) {
                unrecorded.Add(row.Key);
                continue;
            }

            if (!fixtures.ContainsKey(fixture)) {
                unknownFixture.Add(row.Key + " → " + fixture);
                continue;
            }

            foreach (var value in values) {
                if (string.Equals(value.OracleHash, "missing", StringComparison.Ordinal)) {
                    unrecorded.Add(row.Key + " = " + value.Value + " (the oracle produced nothing)");
                    continue;
                }

                // ⚠ The `Agree` flag and not a hash comparison, because agreement is decided after
                // line-ending normalisation and two of the frozen options change nothing else. A row
                // the sweep called agreeing is a row Skala is expected to reproduce.
                var expectation = value.Agree ? FrozenCorpus.Reproduces : FrozenCorpus.Divergent;
                string? divergence = null;

                if (!value.Agree) {
                    // ⚠ A disagreement with no argued entry is refused rather than frozen with an
                    // empty pointer. The frozen file holds the *oracle's* answer, which is a standing
                    // claim that Skala is wrong here; a claim with nothing behind it is the one thing
                    // this corpus must never carry into a world with no oracle to re-ask.
                    if (!divergences.TryGetValue(row.Key, out divergence)) {
                        unargued.Add(row.Key + " = " + value.Value);
                        continue;
                    }
                }

                configurations.Add(
                    new FrozenConfiguration(
                        [new FrozenOverride(row.Key, value.Value)],
                        fixture,
                        FrozenCorpus.PathFor(fixture, value.OracleHash),
                        value.OracleHash,
                        value.SkalaHash,
                        row.Outcome,
                        expectation,
                        divergence,
                        "conformance-sweep.json"
                    )
                );
            }
        }

        if (unrecorded.Count > 0 || unknownFixture.Count > 0 || unargued.Count > 0) {
            log.WriteLine("the committed sweep cannot be frozen as it stands.");
            Report(log, "rows recording nothing to freeze", unrecorded);
            Report(log, "rows naming a fixture the corpus no longer has", unknownFixture);
            Report(
                log,
                "disagreements with no `docs/divergences.md` entry naming the option",
                unargued
            );
            log.WriteLine(
                "\nA divergence the repository has not argued must not be frozen: the frozen file is the "
                + "oracle's answer, and after the oracle is gone the argument is all there is."
            );

            return null;
        }

        return configurations;
    }

    /// <summary>
    ///     Obtains and verifies every distinct output, then writes the corpus — or nothing.
    /// </summary>
    static int Materialise(
        List<FrozenConfiguration> configurations,
        Dictionary<string, CorpusFile> fixtures,
        string version,
        string digest,
        TextWriter log
    ) {
        var targets = configurations
            .DistinctBy(static configuration => configuration.Output, StringComparer.Ordinal)
            .OrderBy(static configuration => configuration.Output, StringComparer.Ordinal)
            .ToArray();

        log.WriteLine(
            $"freeze: {Count(configurations.Count)} configurations over {Count(targets.Length)} distinct outputs"
        );

        var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
        var origins = new Dictionary<string, string>(StringComparer.Ordinal);
        var drifted = new List<string>();

        // ⚠ Skala first, and for every configuration rather than only the ones that might supply an
        // output: the drift check below is the gate, and a gate that only looks at the rows it needs
        // is a gate that passes because it did not look.
        var start = Stopwatch.GetTimestamp();
        foreach (var configuration in configurations) {
            var fixture = fixtures[configuration.Fixture];
            var produced = SkalaSide.Format(
                fixture.Path,
                [.. configuration.Overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            );
            var hash = SkalaSide.Digest(produced);

            if (!string.Equals(hash, configuration.SkalaHash, StringComparison.Ordinal)) {
                drifted.Add(
                    Describe(configuration) + ": the sweep recorded " + configuration.SkalaHash + ", now " + hash
                );
                continue;
            }

            if (string.Equals(hash, configuration.OracleHash, StringComparison.Ordinal)) {
                bodies.TryAdd(configuration.Output, produced);
                origins.TryAdd(configuration.Output, FrozenCorpus.Reproduced);
            }
        }

        log.WriteLine(
            $"  Skala: {Count(configurations.Count)} configurations in "
            + Stopwatch.GetElapsedTime(start).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)
            + " s, "
            + Count(bodies.Count)
            + " outputs reproduced"
        );

        if (drifted.Count > 0) {
            log.WriteLine("\nrefusing to freeze: Skala no longer formats what the committed sweep measured.");
            Report(log, "configurations that have drifted", drifted);
            log.WriteLine(
                "\nThe sweep's verdicts describe a formatter that no longer exists, so its hashes cannot "
                + "authorise a byte. Re-run `./build.sh Sweep` against the oracle, in a commit of its own, "
                + "and freeze from that."
            );

            return 1;
        }

        var outstanding = targets.Where(target => !bodies.ContainsKey(target.Output)).ToArray();
        if (outstanding.Length > 0 && !Measure(outstanding, fixtures, bodies, origins, log)) {
            return 1;
        }

        Write(configurations, targets, fixtures, bodies, origins, version, digest, log);
        return 0;
    }

    /// <summary>
    ///     The outputs no Skala digest can match, asked of <c>jb cleanupcode</c> and checked against the
    ///     digest the committed sweep recorded.
    /// </summary>
    /// <remarks>
    ///     ⚠ One configuration per invocation. It is the slow arrangement and it is the safe one: a
    ///     batch of one is trivially profile-uniform and holds its fixture at most once, which are the
    ///     two properties <see cref="ScratchTree.Batches" /> exists to maintain. There are six of these
    ///     and a freeze is a reviewed action taken rarely, so the cost is minutes rather than a design
    ///     constraint.
    /// </remarks>
    static bool Measure(
        FrozenConfiguration[] outstanding,
        Dictionary<string, CorpusFile> fixtures,
        Dictionary<string, string> bodies,
        Dictionary<string, string> origins,
        TextWriter log
    ) {
        if (OracleRunner.FindExecutableOrNull() is null) {
            log.WriteLine(
                "\nrefusing to freeze: "
                + Count(outstanding.Length)
                + " outputs are on configurations Skala is known to disagree at, so no digest of Skala's can "
                + "match the oracle's and only `jb cleanupcode` can supply them:\n"
            );
            Report(log, "outputs only the oracle can produce", outstanding.Select(Describe));
            log.WriteLine(
                "\n`dotnet tool install -g JetBrains.Skala.GlobalTools --version 2025.2.6`. Freezing "
                + "Skala's answer instead is the one substitution this corpus must never make: it would "
                + "make a known-wrong output the permanent standard."
            );

            return false;
        }

        var runner = new OracleRunner();
        var baseConfig = File.ReadAllText(OracleEditorConfig.Reading(Corpus.OracleEditorConfigPath));
        var refused = new List<string>();

        for (var i = 0; i < outstanding.Length; i++) {
            var configuration = outstanding[i];
            var fixture = fixtures[configuration.Fixture];
            var appended = baseConfig
                + "\n[*.cs]\n"
                + string.Concat(
                    configuration.Overrides.Select(static o => OracleRunner.OracleKey(o.Key) + " = " + o.Value + "\n")
                );

            log.WriteLine($"  oracle {Count(i + 1)}/{Count(outstanding.Length)}: {Describe(configuration)}");
            var produced = ScratchTree.Format(runner, [fixture], _ => appended)[0];

            if (produced is null) {
                refused.Add(Describe(configuration) + ": `cleanupcode` returned nothing");
                continue;
            }

            // ⚠ The same gate the Skala half is held to, and the reason it is not weaker here: this
            // invocation is a *re-measurement*, months after the run that produced the table, and the
            // whole claim of the frozen file is that it holds what that run recorded. An output that
            // does not hash to the recorded digest means the oracle has moved — a JetBrains upgrade,
            // a different machine — and quietly committing it would replace the sweep's evidence with
            // this afternoon's, under the sweep's provenance header.
            var hash = SkalaSide.Digest(produced);
            if (!string.Equals(hash, configuration.OracleHash, StringComparison.Ordinal)) {
                refused.Add(
                    Describe(configuration) + ": the sweep recorded " + configuration.OracleHash + ", now " + hash
                );
                continue;
            }

            bodies[configuration.Output] = produced;
            origins[configuration.Output] = FrozenCorpus.Measured;
        }

        if (refused.Count == 0) {
            return true;
        }

        log.WriteLine("\nrefusing to freeze: the oracle did not reproduce what the committed sweep recorded.");
        Report(log, "outputs whose digest has moved", refused);
        log.WriteLine(
            "\nThe installed `jb` is not the one the sweep was measured against, or the fixture beneath it "
            + "has changed. Re-run `./build.sh Sweep` and freeze from that run, so that one table and one "
            + "corpus describe one oracle."
        );

        return false;
    }

    /// <summary>
    ///     Writes the corpus, having verified all of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The directory is emptied first. A re-freeze that produced fewer outputs would otherwise
    ///     leave the ones it no longer accounts for behind, and an orphaned <c>.expected.cs</c> under
    ///     <see cref="Corpus.Root" /> is a file the provenance tests police and nothing explains.
    /// </remarks>
    static void Write(
        List<FrozenConfiguration> configurations,
        FrozenConfiguration[] targets,
        Dictionary<string, CorpusFile> fixtures,
        Dictionary<string, string> bodies,
        Dictionary<string, string> origins,
        string version,
        string digest,
        TextWriter log
    ) {
        if (Directory.Exists(FrozenCorpus.Root)) {
            Directory.Delete(FrozenCorpus.Root, true);
        }

        var outputs = new List<FrozenOutput>(targets.Length);
        var bytes = 0L;

        foreach (var target in targets) {
            var body = bodies[target.Output];
            var profile = OracleProfile.For(fixtures[target.Fixture]);
            FrozenCorpus.WriteBody(
                Path.Combine(FrozenCorpus.Root, target.Output.Replace('/', Path.DirectorySeparatorChar)),
                body,
                new OracleHeader(version, digest, profile.Name, OracleFixture.Today)
            );

            bytes += body.Length;
            outputs.Add(
                new FrozenOutput(
                    target.Output,
                    target.Fixture,
                    profile.Name,
                    target.OracleHash,
                    body.Length,
                    origins[target.Output]
                )
            );
        }

        FrozenCorpus.WriteManifest(
            FrozenCorpus.ManifestPath,
            new FrozenManifest(
                new FrozenProvenance(version, digest, Commit(), OracleFixture.Today, "conformance-sweep.json"),
                outputs,
                [
                    .. configurations
                        .OrderBy(static c => c.Overrides[0].Key, StringComparer.Ordinal)
                        .ThenBy(static c => c.Overrides[0].Value, StringComparer.Ordinal)
                ]
            )
        );

        var measured = Enumerable.Count(outputs, static output => output.Origin == FrozenCorpus.Measured);
        log.WriteLine(
            $"\nfroze {Count(outputs.Count)} outputs ({Count((int)bytes)} bytes) over "
            + $"{Count(configurations.Count)} configurations into {FrozenCorpus.Root}"
        );
        log.WriteLine(
            $"  {Count(outputs.Count - measured)} reproduced by Skala at the oracle's recorded digest, "
            + $"{Count(measured)} measured from `jb cleanupcode`"
        );
        log.WriteLine(
            "  "
            + Count(Enumerable.Count(configurations, static c => c.Expectation == FrozenCorpus.Divergent))
            + " configurations are frozen as expected-to-fail targets, each naming its `docs/divergences.md` entry"
        );
    }

    /// <summary>The commit the freeze ran on, or a marker when git cannot say.</summary>
    /// <remarks>
    ///     ⚠ Recorded rather than assumed to be recoverable from the commit that adds the files. A
    ///     corpus is re-frozen in one commit and moved in another, and "which tree was this measured
    ///     on" is the question the provenance exists to answer.
    /// </remarks>
    static string Commit() {
        try {
            using var process = Process.Start(
                new ProcessStartInfo("git", "rev-parse HEAD") {
                    WorkingDirectory = Corpus.RepositoryRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            );

            if (process is null) {
                return "unknown";
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && output.Length > 0 ? output : "unknown";
        } catch (Exception exception) when (exception is SystemException or InvalidOperationException) {
            return "unknown";
        }
    }

    static string Describe(FrozenConfiguration configuration) =>
        string.Join(", ", configuration.Overrides.Select(static o => o.Key + " = " + o.Value))
        + " on "
        + configuration.Fixture;

    static string? Recorded(string text, string pattern) {
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    static void Report(TextWriter log, string what, IEnumerable<string> lines) {
        var listed = lines.ToArray();
        if (listed.Length == 0) {
            return;
        }

        log.WriteLine("\n  " + Count(listed.Length) + " " + what + ":");
        foreach (var line in listed.Take(Examples)) {
            log.WriteLine("    " + line);
        }

        if (listed.Length > Examples) {
            log.WriteLine("    …");
        }
    }

    static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
///     Which <c>docs/divergences.md</c> entry argues a given option's disagreement.
/// </summary>
/// <remarks>
///     ⚠ Parsed from the document rather than held as a table here. A second list of "which entry owns
///     which key" is a list that drifts from the document it describes — <c>OptionDomain</c>'s remarks
///     record five hand-kept copies of one fact and what invalidating them at once cost — and this one
///     would drift silently, because a frozen file pointing at a renumbered entry still looks fine.
/// </remarks>
public static class Divergences {
    /// <summary>Option key to the first entry that lists it, in document order.</summary>
    public static IReadOnlyDictionary<string, string> Read(string path) {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) {
            return owners;
        }

        var entry = string.Empty;
        foreach (var line in File.ReadLines(path)) {
            var heading = Regex.Match(line, @"^## (?<id>SK-DIV-\d+)");
            if (heading.Success) {
                entry = heading.Groups["id"].Value;
                continue;
            }

            if (entry.Length == 0 || !line.StartsWith("- options:", StringComparison.Ordinal)) {
                continue;
            }

            foreach (Match option in Regex.Matches(line, "`(?<key>[a-z0-9_]+)`")) {
                owners.TryAdd(option.Groups["key"].Value, entry);
            }
        }

        return owners;
    }
}
