using System.Globalization;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     The key-flip sweep, replayed from committed bytes with no oracle in the room.
/// </summary>
/// <remarks>
///     ⚠ <b>This is the test the repository runs after ReSharper is uninstalled.</b> Two conformance
///     guarantees stand today and until this file existed only one of them was standalone. The
///     <c>.expected.cs</c> fixtures pin Skala at the values in the export and need nothing but their own
///     bytes. The key-flip sweep pins Skala at <em>every other</em> value — <c>indent_style = tab</c>,
///     <c>max_line_length = 1</c>, every enum member of every placement key — and it commits
///     <em>verdicts</em>, throwing the outputs away. That guarantee lives in <c>jb cleanupcode</c>, and
///     it evaporates the moment <c>jb</c> is gone: a regression at a non-export value would leave
///     every instrument in this repository green.
///     <para>
///         ⚠ <b>What makes replaying it honest.</b> <c>ProvenanceTests.TheCommittedSweep_MeasuredTheFormatterInForce</c>
///         already re-asks Skala at every swept configuration — but it compares against <c>SkalaHash</c>,
///         which is <em>Skala's own recorded answer</em>. That catches drift and cannot catch a wrong
///         answer: if Skala's output at <c>indent_style = tab</c> were wrong when the sweep ran, it would
///         be wrong and green forever. This file compares against the frozen bytes, whose digests are the
///         <em>oracle's</em>. The two tests look alike and are the two halves of the claim: one says
///         "Skala has not moved", the other says "and where it stands is where ReSharper stood".
///     </para>
///     <para>
///         ⚠ <b>It does not regenerate itself, and here that rule is load-bearing rather than tidy.</b>
///         <c>build/Build.cs</c>'s <c>Oracle</c> target states it — "an oracle that updates itself when it
///         disagrees is a tautology" — and after retirement there is no second opinion to restore what a
///         self-healing corpus would erase. Regeneration is
///         <c>dotnet run --project Testing/Rikarin.Skala.Conformance.Sweep -- freeze</c>, a deliberate
///         action in a reviewed commit whose diff is the review, and it refuses to write a byte that does
///         not hash to what <c>conformance-sweep.json</c> recorded.
///     </para>
///     <para>
///         ⚠ <b>What this corpus cannot answer, stated here rather than discovered later.</b> It is a
///         recording, not the oracle. It can say "Skala still produces at this configuration what
///         ReSharper produced at it in August 2026". It cannot answer a question nobody asked ReSharper
///         then: a value outside <c>OptionDomain.Probes</c>, a fixture added after the freeze, a pair of
///         keys flipped together, or any option the sweep excluded. Those are permanently unanswerable
///         once the oracle is gone — not by this corpus and not by any other — and the only defence is
///         that the sweep is re-run and re-frozen while <c>jb</c> is still installed.
///     </para>
/// </remarks>
public sealed class FrozenSweepTests {
    /// <summary>How many failures to name before falling back to a count.</summary>
    const int Examples = 10;

    /// <summary>
    ///     Every frozen configuration replays to the bytes the oracle produced for it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is byte equality against the frozen body, not a hash comparison against the
    ///     manifest. The manifest's digests are an index; the bytes are the evidence, and a test that
    ///     only checked digests would pass over a corpus whose files had been emptied.
    /// </remarks>
    [Fact]
    public void EveryFrozenConfiguration_ReplaysWithoutAnOracle() {
        var manifest = Manifest();
        var fixtures = Corpus.All().ToDictionary(static file => file.ToString(), StringComparer.Ordinal);

        var wrong = new List<string>();
        var closed = new List<string>();
        var replayed = 0;

        foreach (var configuration in manifest.Configurations) {
            Assert.True(
                fixtures.TryGetValue(configuration.Fixture, out var fixture),
                configuration.Fixture
                + " is frozen but the corpus no longer has it, so its configurations cannot be replayed."
            );

            var frozen = Path.Combine(
                FrozenCorpus.Root,
                configuration.Output.Replace('/', Path.DirectorySeparatorChar)
            );
            Assert.True(File.Exists(frozen), configuration.Output + " is in the manifest and not on disk.");

            var body = FrozenCorpus.ReadBody(frozen);
            var produced = SkalaSide.Format(
                fixture!.Path,
                [.. configuration.Overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            );
            replayed++;

            // ⚠ Normalised, on the same argument every comparison in this repository makes: a frozen
            // file may have been written on another OS. The two options whose entire effect is line
            // endings are `Divergent` or `Unexercised` in the committed sweep and are covered by the
            // raw-byte assertion in TheFrozenCorpus_IsInternallyConsistent, which compares each file
            // against the digest the oracle recorded for it.
            var matches = string.Equals(
                TextNormalisation.Normalise(produced),
                TextNormalisation.Normalise(body),
                StringComparison.Ordinal
            );

            if (configuration.Expectation == FrozenCorpus.Divergent) {
                // ⚠ An expected-to-fail target, and a *pass* here is the finding. The frozen file is
                // the oracle's answer at a configuration Skala is argued to get wrong; Skala matching
                // it means the argument has been overtaken by a fix, and the entry and the row both
                // need to move. Silently tolerating it would leave a closed divergence permanently
                // recorded as open.
                if (matches) {
                    closed.Add(
                        Describe(configuration)
                        + " now matches the oracle — "
                        + (configuration.Divergence ?? "its divergence")
                        + " looks closed"
                    );
                }

                continue;
            }

            if (!matches) {
                wrong.Add(Describe(configuration) + " → " + configuration.Output);
            }
        }

        // ⚠ The population canary, in the shape KeyFlipSweep.IsBrokenMeasurement names. Every
        // assertion above is satisfied by an empty manifest, and that is the one reading which means
        // this test measured nothing at all.
        Assert.True(
            replayed > 0,
            "the frozen sweep corpus replayed no configuration, so this test asserted nothing."
        );

        Assert.True(
            wrong.Count == 0,
            Count(wrong.Count)
            + " of "
            + Count(replayed)
            + " frozen configurations no longer format the way `jb cleanupcode` formatted them:\n"
            + Sample(wrong)
            + "\n\nThese are the configurations the export does not use, so no `.expected.cs` fixture "
            + "covers them and nothing else in the suite can see this.\n"
            + "⚠ Do not re-freeze to make this green. The frozen bytes are the oracle's answer and this "
            + "is a regression until somebody has shown otherwise; re-freezing would make Skala's new "
            + "answer the standard, which is the tautology docs/plan/12 § \"The oracle\" forbids."
        );

        Assert.True(
            closed.Count == 0,
            Count(closed.Count)
            + " frozen divergences no longer reproduce — Skala now agrees with the oracle where an "
            + "argued entry says it does not:\n"
            + Sample(closed)
            + "\n\nThat is good news and it still fails, because the entry in docs/divergences.md and "
            + "the sweep's row both still say otherwise. Close the entry, re-run `./build.sh Sweep` so "
            + "the row becomes CONFORMANT, and re-freeze from that run."
        );
    }

    /// <summary>
    ///     The frozen corpus is a claim about the configuration and the oracle still on disk.
    /// </summary>
    /// <remarks>
    ///     ⚠ The same check <c>ProvenanceTests</c> makes for the fixtures and for the sweep report, and
    ///     it matters more here than in either. Both sides of a frozen comparison are committed bytes,
    ///     so when the base configuration moves, nothing in this file's replay changes and the whole
    ///     corpus quietly becomes a record of a configuration nobody is using — green, and measuring the
    ///     wrong thing. That failure has happened once already (<c>076fde6</c>) and it surfaces only
    ///     here.
    ///     <para>
    ///         ⚠ The per-file headers are policed by
    ///         <c>ProvenanceTests.EveryFixture_RecordsTheConfigurationInForce</c> for free, because the
    ///         frozen bodies live under <c>Testing/corpus/</c> and are named <c>*.expected.cs</c>. What is
    ///         left for this test is the manifest, which that walk does not read.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheFrozenCorpus_RecordsWhatItWasMeasuredAgainst() {
        var manifest = Manifest();
        var provenance = manifest.Provenance;

        var inForce = OracleFixture.ConfigDigestInForce();
        Assert.True(
            string.Equals(provenance.ConfigDigest, inForce, StringComparison.Ordinal),
            "the frozen sweep corpus was measured against a configuration that is no longer in force.\n\n"
            + "  "
            + Corpus.BaseEditorConfigPath
            + "\n  on disk now:         sha256:"
            + inForce
            + "\n  the corpus records:  sha256:"
            + provenance.ConfigDigest
            + "\n\nBoth sides of every frozen comparison are committed bytes, so this cannot surface as a "
            + "replay failure — it surfaces only here. Either restore the configuration, or re-run "
            + "`./build.sh Sweep` and re-freeze, in a reviewed commit of its own."
        );

        Assert.True(
            provenance.ReSharperVersion.Length > 0,
            FrozenCorpus.ManifestPath + " records no ReSharper version, so what produced these bytes is unrecorded."
        );

        Assert.True(
            provenance.Commit.Length > 0 && provenance.Frozen.Length > 0,
            FrozenCorpus.ManifestPath + " records no commit or no date, so when these bytes were frozen is unrecorded."
        );

        // ⚠ One oracle across the frozen corpus and the fixtures beside it. Fixtures from two
        // ReSharper versions are not comparable, and a corpus frozen under one while the
        // `.expected.cs` files record another is two standards wearing one provenance.
        var versions = Corpus.Fixtures()
            .Select(HeaderVersionOf)
            .Where(static version => version is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            versions.Length == 1 && string.Equals(versions[0], provenance.ReSharperVersion, StringComparison.Ordinal),
            "the frozen sweep corpus records resharper="
            + provenance.ReSharperVersion
            + " and the committed fixtures record "
            + string.Join(", ", versions.Select(static version => "resharper=" + version))
            + ". Two oracles is two standards; re-run `./build.sh Oracle` and `./build.sh Sweep` under one."
        );
    }

    /// <summary>
    ///     Every frozen file hashes to the digest the sweep recorded, and nothing is orphaned.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This is what makes the corpus self-authenticating.</b> The digests in the manifest were
    ///     not computed from these files — they were copied out of <c>conformance-sweep.json</c>, which
    ///     was written by a run that had <c>jb cleanupcode</c> in front of it. Re-deriving them here
    ///     therefore checks the bytes against the oracle's own recorded answer rather than against
    ///     themselves, and a file edited by hand after the freeze fails, digest and all.
    ///     <para>
    ///         ⚠ Raw bytes here, deliberately, where the replay above normalises. This assertion is the
    ///         one place the line-ending and final-newline options are actually pinned: the sweep had to
    ///         read their verdicts off the raw bytes for the same reason, and a normalised check would
    ///         erase their entire effect.
    ///     </para>
    ///     <para>
    ///         ⚠ Both directions. A frozen file no configuration points at is not harmless: it is a
    ///         committed oracle output that nothing replays, which is indistinguishable from a
    ///         guarantee until somebody looks.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheFrozenCorpus_IsInternallyConsistent() {
        var manifest = Manifest();

        var corrupt = new List<string>();
        var missing = new List<string>();

        foreach (var output in manifest.Outputs) {
            var path = Path.Combine(FrozenCorpus.Root, output.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) {
                missing.Add(output.Path);
                continue;
            }

            var body = FrozenCorpus.ReadBody(path);
            var digest = SkalaSide.Digest(body);
            if (!string.Equals(digest, output.OracleHash, StringComparison.Ordinal)) {
                corrupt.Add(
                    output.Path + ": the sweep recorded " + output.OracleHash + ", the file hashes to " + digest
                );
            }
        }

        Assert.True(
            manifest.Outputs.Count > 0,
            FrozenCorpus.ManifestPath + " indexes no outputs. That is a broken corpus, not a clean one."
        );

        Assert.True(
            missing.Count == 0,
            Count(missing.Count)
            + " frozen outputs are in the manifest and not on disk:\n"
            + Sample(missing)
            + "\n\nRe-freeze with `dotnet run --project Testing/Rikarin.Skala.Conformance.Sweep -- freeze`."
        );

        Assert.True(
            corrupt.Count == 0,
            Count(corrupt.Count)
            + " of "
            + Count(manifest.Outputs.Count)
            + " frozen outputs no longer hash to the digest `jb cleanupcode` produced for them:\n"
            + Sample(corrupt)
            + "\n\nThese files were edited after they were frozen. They are not Skala's output and must "
            + "never be corrected to match it — restore them from git, or re-freeze from a fresh "
            + "`./build.sh Sweep`."
        );

        // ⚠ Both directions, from the filesystem rather than from the manifest, on the same argument
        // Corpus.Fixtures makes: a file no enumeration claims is exactly the file nothing checks.
        var indexed = manifest.Outputs.Select(static output => output.Path).ToHashSet(StringComparer.Ordinal);
        var orphans = FrozenCorpus.Bodies().Where(path => !indexed.Contains(path)).ToArray();

        Assert.True(
            orphans.Length == 0,
            Count(orphans.Length)
            + " frozen files under "
            + FrozenCorpus.Root
            + " are in no manifest row, so nothing replays them:\n"
            + Sample(orphans)
            + "\n\nA committed oracle output that nothing replays looks like a guarantee and is not one. "
            + "Re-freeze, which rewrites the directory from the manifest it writes."
        );

        var referenced = manifest.Configurations.Select(static c => c.Output).ToHashSet(StringComparer.Ordinal);
        var unreplayed = manifest.Outputs
            .Select(static output => output.Path)
            .Where(path => !referenced.Contains(path))
            .ToArray();

        Assert.True(
            unreplayed.Length == 0,
            Count(unreplayed.Length)
            + " frozen outputs are indexed but no configuration produces them:\n"
            + Sample(unreplayed)
            + "\n\nRe-freeze; the manifest and the bytes are written together and should not disagree."
        );
    }

    /// <summary>
    ///     Every divergent row names an entry that <c>docs/divergences.md</c> actually has.
    /// </summary>
    /// <remarks>
    ///     ⚠ A frozen divergence is a standing claim that Skala is wrong at a configuration, and its
    ///     only support is the argument the row points at. The freeze refuses to write one without a
    ///     pointer; nothing until this test checked that the pointer still resolves. An entry can be
    ///     renumbered or removed long after the freeze, and a dangling one fails in the worst possible
    ///     way — the file still looks authoritative and there is no longer an oracle to re-ask.
    /// </remarks>
    [Fact]
    public void EveryFrozenDivergence_NamesAnArgumentThatStillExists() {
        var manifest = Manifest();
        var document = File.ReadAllText(Path.Combine(Corpus.RepositoryRoot, "docs", "divergences.md"));

        var divergent = manifest.Configurations
            .Where(static configuration => configuration.Expectation == FrozenCorpus.Divergent)
            .ToArray();

        // ⚠ The population canary again. The committed sweep has 8 non-conformant options and if this
        // set is ever empty, the likeliest cause is that the freeze stopped classifying rather than
        // that every divergence closed.
        Assert.True(
            divergent.Length > 0,
            "no frozen configuration is marked "
            + FrozenCorpus.Divergent
            + ". The committed sweep has non-conformant rows, so an empty set here means the freeze "
            + "stopped classifying them — which would silently freeze Skala's answer as the standard."
        );

        var dangling = divergent
            .Where(configuration => configuration.Divergence is null
                || !document.Contains("## " + configuration.Divergence + " ", StringComparison.Ordinal)
            )
            .Select(static configuration => Describe(configuration) + " → " + (configuration.Divergence ?? "(none)"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            dangling.Length == 0,
            Count(dangling.Length)
            + " frozen divergences name a docs/divergences.md entry that is not there:\n"
            + Sample(dangling)
            + "\n\nThe frozen file holds the oracle's answer and the entry is the whole argument for "
            + "keeping it as the target. Restore the entry, or re-freeze so the row points at the one "
            + "that replaced it."
        );
    }

    static FrozenManifest Manifest() {
        var manifest = FrozenCorpus.ReadManifest(FrozenCorpus.ManifestPath);
        Assert.True(
            manifest is not null,
            "there is no frozen sweep corpus at "
            + FrozenCorpus.ManifestPath
            + ".\n\nIt is what makes the key-flip sweep's guarantee survive ReSharper's uninstallation, "
            + "and without it the only thing pinning Skala away from the export's own values is "
            + "`jb cleanupcode` itself. Write it with "
            + "`dotnet run --project Testing/Rikarin.Skala.Conformance.Sweep -- freeze`."
        );

        return manifest!;
    }

    static string? HeaderVersionOf(string path) {
        using var reader = new StreamReader(path);
        return reader.ReadLine() is { } line ? OracleHeader.Parse(line)?.ReSharperVersion : null;
    }

    static string Describe(FrozenConfiguration configuration) =>
        string.Join(", ", configuration.Overrides.Select(static o => o.Key + " = " + o.Value))
        + " on "
        + configuration.Fixture;

    static string Sample(IEnumerable<string> lines) {
        var listed = lines.Take(Examples + 1).ToArray();
        return string.Join("\n", listed.Take(Examples).Select(static line => "  " + line))
            + (listed.Length > Examples ? "\n  …" : string.Empty);
    }

    static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
