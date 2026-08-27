using System.Globalization;
using System.Text.RegularExpressions;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     Every committed measurement is a measurement of the configuration that is still on disk.
/// </summary>
/// <remarks>
///     ⚠ <b>The hole this closes.</b> Each oracle fixture carries a provenance header naming the
///     ReSharper version and the digest of the base <c>.editorconfig</c> it was generated under, and
///     until this file existed <em>nothing in the repository read it</em>. The consequence is not
///     hypothetical and it is not detectable by any other test here: the whole suite compares Skala's
///     output against frozen fixtures, so when the base configuration changes, both sides of every
///     comparison stay exactly where they were and the suite stays green while every fixture in the
///     corpus silently becomes a record of a configuration that no longer exists.
///     <para>
///         It happened. At <c>076fde6</c> the base configuration was stripped of ~1 997 keys by hand and
///         its digest moved from <c>98ff5257</c> to <c>bd9791d3</c>; the corpus and the committed sweep
///         report both still said <c>98ff5257</c>, and the build was green. What follows is the instrument
///         that would have said so on the first run after that commit.
///     </para>
///     <para>
///         ⚠ These tests deliberately do <b>not</b> regenerate anything. An oracle that refreshes itself
///         when it disagrees is a tautology (docs/plan/12 § "The oracle"), and that argument applies with
///         more force to provenance than to content: a header that re-stamps itself records nothing at
///         all. Both failures below say what to run and leave the running to a person.
///     </para>
/// </remarks>
public sealed class ProvenanceTests {
    /// <summary>How many offending fixtures to name before falling back to a count.</summary>
    /// <remarks>
    ///     ⚠ There are ~2 000 fixtures and the realistic failure moves all of them at once, so the
    ///     message has to be a summary rather than a listing. A test whose output is two thousand lines
    ///     is a test nobody reads to the end of, and the count is the part that says what happened.
    /// </remarks>
    const int Examples = 5;

    /// <summary>
    ///     Every fixture records the digest of the base configuration as it stands on disk.
    /// </summary>
    /// <remarks>
    ///     ⚠ A fixture with no header is a failure and not a skip. "Missing means nobody measured" is
    ///     the standing convention here — <c>OptionCoverageTests.SweepUnsubstantiated</c> makes the same
    ///     move for a missing sweep report, returning empty so that the strict invariant is *restored*
    ///     rather than quietly relaxed. A header-less fixture is a file claiming to be an oracle
    ///     measurement while declining to say what it measured, which is the weaker of the two states
    ///     this test exists to reject.
    /// </remarks>
    [Fact]
    public void EveryFixture_RecordsTheConfigurationInForce() {
        var fixtures = Corpus.Fixtures();

        // ⚠ The population canary, in the shape KeyFlipSweep.IsBrokenMeasurement names: a test that
        // asserts something of every member of an empty set passes for the one reason that means it
        // measured nothing. The corpus is ~2 000 fixtures; zero is a broken enumeration.
        Assert.True(
            fixtures.Count > 0,
            $"no `*.expected.cs` found under {Corpus.Root}. That is a broken enumeration, not a clean corpus."
        );

        var inForce = OracleFixture.ConfigDigestInForce();
        var headerless = new List<string>();
        var mismatched = new List<(string Path, string Recorded)>();

        foreach (var fixture in fixtures) {
            var header = HeaderOf(fixture);
            if (header is null) {
                headerless.Add(fixture);
                continue;
            }

            if (!string.Equals(header.ConfigHash, inForce, StringComparison.Ordinal)) {
                mismatched.Add((fixture, header.ConfigHash));
            }
        }

        Assert.True(
            headerless.Count == 0,
            Count(headerless.Count)
            + " of "
            + Count(fixtures.Count)
            + " committed fixtures carry no `"
            + OracleHeader.Prefix
            + "` header, so what they measured is unrecorded:\n"
            + Sample(headerless.Select(Relative))
            + "\n\nRegenerate them with `./build.sh Oracle`, which stamps the header as it writes."
        );

        Assert.True(
            mismatched.Count == 0,
            Count(mismatched.Count)
            + " of "
            + Count(fixtures.Count)
            + " committed fixtures were generated under a base configuration that is no longer in force.\n\n"
            + "  "
            + Corpus.BaseEditorConfigPath
            + "\n  on disk now: sha256:"
            + inForce
            + "\n"
            + Sample(mismatched.Select(entry => Relative(entry.Path) + " records sha256:" + entry.Recorded))
            + "\n\nThe fixtures are frozen and so is Skala's side of every comparison, so this cannot "
            + "surface as a content failure — it surfaces only here.\n"
            + "Either restore the configuration, or regenerate the corpus with `./build.sh Oracle` in a "
            + "reviewed commit of its own."
        );
    }

    /// <summary>
    ///     One ReSharper version across the whole corpus.
    /// </summary>
    /// <remarks>
    ///     ⚠ Fixtures from two ReSharper versions are not comparable, and nothing else notices. The
    ///     fidelity number is a count over the whole corpus; half-regenerating it after a JetBrains
    ///     upgrade produces a corpus that is internally inconsistent and a number that is an average of
    ///     two different oracles. Any divergence class read off it afterwards is attributed to Skala
    ///     when the cause was the tool moving underneath.
    ///     <para>
    ///         The assertion is uniformity rather than a pinned version string. Upgrading the oracle is a
    ///         legitimate, deliberate act; doing half of it is not.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryFixture_RecordsTheSameReSharperVersion() {
        var fixtures = Corpus.Fixtures();
        Assert.True(fixtures.Count > 0, $"no `*.expected.cs` found under {Corpus.Root}.");

        var byVersion = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var fixture in fixtures) {
            var version = HeaderOf(fixture)?.ReSharperVersion ?? "(no header)";
            if (!byVersion.TryGetValue(version, out var files)) {
                byVersion[version] = files = [];
            }

            files.Add(fixture);
        }

        Assert.True(
            byVersion.Count == 1,
            "the corpus was generated by "
            + Count(byVersion.Count)
            + " different ReSharper versions, and fixtures from two versions are not comparable:\n"
            + string.Join(
                "\n",
                byVersion.Select(entry =>
                    "  resharper="
                    + entry.Key
                    + ": "
                    + Count(entry.Value.Count)
                    + " fixtures, e.g. "
                    + Relative(entry.Value[0])
                )
            )
            + "\n\nRegenerate the whole corpus under one version with `./build.sh Oracle`."
        );
    }

    /// <summary>
    ///     The committed key-flip sweep measured the configuration that is still on disk.
    /// </summary>
    /// <remarks>
    ///     ⚠ The sweep's table is load-bearing rather than informational.
    ///     <c>OptionCoverageTests.TierA_IsWhatSkalaReads_AndTheSweepSubstantiates</c> and
    ///     <c>TierD_CarriesAFixtureOnlyWhereTheSweepDemotedIt</c> both read
    ///     <c>conformance-sweep.json</c> and let it decide which tier an option is entitled to. A sweep
    ///     run against a configuration that has since changed is therefore not a stale note in a report:
    ///     it is the evidence two live invariants are resting on.
    ///     <para>
    ///         ⚠ The digest is read out of the <c>.md</c> because that is the only one of the pair that
    ///         records it — the <c>.json</c>, which is the file the tests actually read, carries no
    ///         provenance at all. That asymmetry is why the two files are asserted to travel together
    ///         below: the machine-readable half cannot be trusted further than the human-readable half's
    ///         header.
    ///     </para>
    ///     <para>
    ///         ⚠ A missing report is not a pass here, in the same sense that it is not a pass in
    ///         <c>SweepUnsubstantiated</c>. There it returns empty and *restores* the strict tier
    ///         invariant; here there is simply nothing that claims to have been measured, so there is
    ///         nothing to catch out. What must never be tolerated is the state in between — a
    ///         <c>.json</c> the tier tests believe with no <c>.md</c> beside it to say what it was
    ///         measured against.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheCommittedSweep_WasMeasuredAgainstTheConfigurationInForce() {
        var directory = Path.Combine(Corpus.RepositoryRoot, "Testing", "Rikarin.Skala.Conformance.Sweep");
        var json = Path.Combine(directory, "conformance-sweep.json");
        var markdown = Path.Combine(directory, "conformance-sweep.md");

        if (!File.Exists(json) && !File.Exists(markdown)) {
            return;
        }

        Assert.True(
            File.Exists(markdown),
            "conformance-sweep.json is committed and the tier tests read it, but conformance-sweep.md is "
            + "not there — and the .md is the only half of the pair that records which base configuration "
            + "the run measured. Re-run ./build.sh Sweep, which writes both."
        );

        var recorded = Regex.Match(
            File.ReadAllText(markdown),
            @"base configuration \|[^|]*sha256 `(?<digest>[0-9a-f]+)`"
        );

        Assert.True(
            recorded.Success,
            markdown
            + " records no base-configuration digest, so what it measured is unrecorded. Re-run "
            + "./build.sh Sweep."
        );

        var inForce = OracleFixture.ConfigDigestInForce();
        Assert.True(
            string.Equals(recorded.Groups["digest"].Value, inForce, StringComparison.Ordinal),
            "the sweep was measured against a configuration that is no longer in force; re-run "
            + "./build.sh Sweep.\n\n"
            + "  "
            + Corpus.BaseEditorConfigPath
            + "\n  on disk now:      sha256:"
            + inForce
            + "\n  the sweep records: sha256:"
            + recorded.Groups["digest"].Value
            + "\n\nUntil it is re-run, `OptionCoverageTests` is deciding tiers from measurements of a "
            + "configuration nobody is using."
        );
    }

    static OracleHeader? HeaderOf(string path) {
        using var reader = new StreamReader(path);
        return reader.ReadLine() is { } line ? OracleHeader.Parse(line) : null;
    }

    static string Relative(string path) => Path.GetRelativePath(Corpus.Root, path).Replace('\\', '/');

    static string Sample(IEnumerable<string> lines) {
        var listed = lines.Take(Examples + 1).ToArray();
        return string.Join("\n", listed.Take(Examples).Select(static line => "  " + line))
            + (listed.Length > Examples ? "\n  …" : string.Empty);
    }

    static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
