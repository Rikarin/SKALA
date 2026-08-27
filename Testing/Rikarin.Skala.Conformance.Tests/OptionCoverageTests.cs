using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     Level 1 of docs/plan/12: the option units, and the floor under Tier A.
/// </summary>
/// <remarks>
///     ⚠ "Every entry in <c>options.json</c> requires at least one corpus file in <c>constructs/</c>
///     that changes behaviour when the option changes." An option with no observable effect is either
///     unimplemented or wrongly wired — both are bugs, and both are build failures here.
///     <para>
///         The test is scoped to the options the formatter actually reads. It is inert for the rest, which
///         is the honest state of a milestone that implements 138 of ~380 keys, and it becomes a wider net
///         with no change to this file as each later phase promotes its own.
///     </para>
/// </remarks>
public sealed class OptionCoverageTests {
    public static TheoryData<string> ImplementedKeys {
        get {
            var data = new TheoryData<string>();
            foreach (var id in PhaseOneOptions.Implemented) {
                data.Add(OptionRegistry.Get(id).Key);
            }

            return data;
        }
    }

    /// <summary>
    ///     Every option Skala implements: the formatter's and the arranger's.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two sets, one claim. Milestone 4 added a second component that reads options, and folding
    ///     its keys into <see cref="PhaseOneOptions.Implemented" /> would have made
    ///     <see cref="EveryImplementedOption_ChangesTheOutputOfItsCorpusFile" /> unprovable for a dozen
    ///     of them — that test formats a file, and an arrangement key changes nothing about formatting.
    ///     The honest shape is to keep the sets apart and measure each against the component that
    ///     implements it, while Tier A stays one claim about the whole tool.
    /// </remarks>
    static HashSet<OptionId> Implemented() => [
        .. PhaseOneOptions.Implemented,
        .. Rikarin.Skala.Formatting.CSharp.Arrangement.ArrangementOptions.Implemented
    ];

    [Fact]
    public void TierA_IsWhatSkalaReads_AndTheSweepSubstantiates() {
        // ⚠ Tier A means "Skala reproduces Rider's behaviour, pinned by at least one oracle
        // fixture" (docs/plan/03 § "Four tiers"). It may not rest on a default being known:
        // defaultSource is `template` or `unknown` for every entry in the registry, so the only
        // available evidence is a fixture (docs/plan/03 § "distill", corrected in d081293).
        var implemented = Implemented();
        var claimed = OptionRegistry.All.Where(static info => info.Tier == OptionTier.A)
            .Select(static info => info.Id)
            .ToHashSet();

        var overclaimed = claimed.Except(implemented)
            .Select(static id => OptionRegistry.Get(id).Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(overclaimed.Length == 0, "Tier A without an implementation: " + string.Join(", ", overclaimed));

        // ⚠ "Reads the key" is a weaker claim than Tier A, and this direction used to conflate the
        // two. Tier A is "Skala reproduces *Rider's* behaviour"; a key the formatter reads, and acts
        // on, and acts on differently from ReSharper satisfies the first and fails the second. Only
        // the key-flip sweep can tell them apart — it is the one measurement that flips each option
        // and compares both engines — and on the run committed beside it, 70 options that this test
        // would have called Tier A produced output the oracle does not produce.
        //
        // So an implemented option must be Tier A *unless* the committed sweep says it is not
        // conformant. The sweep needs JetBrains and takes minutes (ADR-011), so what is read here is
        // its committed table, exactly as the fast path reads the oracle fixtures.
        var underclaimed = implemented.Except(claimed)
            .Except(SweepUnsubstantiated())
            .Select(static id => OptionRegistry.Get(id).Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(underclaimed.Length == 0, "Implemented but not Tier A: " + string.Join(", ", underclaimed));
    }

    /// <summary>
    ///     A Tier D option may keep its <c>oracle</c> fixture only where the sweep demoted it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The other half of a rule <c>OptionRegistryTests.Tiers_AreHonest</c> used to state as "no
    ///     Tier D entry carries a glob at all". That was right until the sweep started demoting options
    ///     that have fixtures and fail on them, and it cannot be checked in the registry's own test
    ///     project, which cannot reach the sidecar.
    ///     <para>
    ///         ⚠ Both directions matter. A glob on an undemoted Tier D entry is the original defect — a
    ///         promotion nobody made. A demoted entry that *lost* its glob is the worse one: the sweep only
    ///         sweeps options that have one, so the option would silently leave the next run and its
    ///         demotion could never be reversed or disproved.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TierD_CarriesAFixtureOnlyWhereTheSweepDemotedIt() {
        var unsubstantiated = SweepUnsubstantiated();

        var unexplained = OptionRegistry.All
            .Where(info => info.Tier == OptionTier.D
                && info.Oracle is { Length: > 0 }
                && !unsubstantiated.Contains(info.Id)
            )
            .Select(static info => info.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            unexplained.Length == 0,
            "Tier D with an `oracle` fixture the sweep does not account for: " + string.Join(", ", unexplained)
        );

        var stripped = unsubstantiated
            .Select(static id => OptionRegistry.Get(id))
            .Where(static info => info.Tier == OptionTier.D && info.Oracle is not { Length: > 0 })
            .Select(static info => info.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            stripped.Length == 0,
            "Demoted by the sweep but no longer fixtured, so the next sweep cannot re-measure it: "
            + string.Join(", ", stripped)
        );
    }

    /// <summary>
    ///     The options the last committed sweep could not substantiate, and which are therefore
    ///     deliberately not Tier A however much of them the formatter reads.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read from the committed sidecar rather than measured here. Re-running the sweep needs
    ///     JetBrains installed and minutes of wall clock; the committed table is the artefact the fast
    ///     path reviews in a diff, and an option that becomes conformant again is promoted by the same
    ///     diff that records the measurement.
    ///     <para>
    ///         ⚠ Missing file means "nobody has measured", not "everything is fine": it returns empty, which
    ///         puts the strict invariant back rather than relaxing it silently.
    ///     </para>
    /// </remarks>
    static HashSet<OptionId> SweepUnsubstantiated() {
        var path = Path.Combine(
            Corpus.RepositoryRoot,
            "Testing",
            "Rikarin.Skala.Conformance.Sweep",
            "conformance-sweep.json"
        );

        if (!File.Exists(path)) {
            return [];
        }

        var unsubstantiated = new HashSet<OptionId>();
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        foreach (var row in document.RootElement.EnumerateArray()) {
            if (string.Equals(row.GetProperty("Outcome").GetString(), "Conformant", StringComparison.Ordinal)) {
                continue;
            }

            if (OptionRegistry.TryResolve(row.GetProperty("Key").GetString() ?? string.Empty, out var id)) {
                unsubstantiated.Add(id);
            }
        }

        return unsubstantiated;
    }

    [Fact]
    public void NoOptionClaimsTierB_WithoutADocumentedDivergence() {
        foreach (var info in OptionRegistry.All.Where(static i => i.Tier == OptionTier.B)) {
            Assert.True(
                Divergences.Register.Any(entry => entry.Options.Contains(info.Key, StringComparer.Ordinal)),
                $"{info.Key} is Tier B, which means 'implemented with a documented divergence'. docs/divergences.md does not mention it."
            );
        }
    }

    public static TheoryData<string> ArrangementKeys {
        get {
            var data = new TheoryData<string>();
            foreach (var id in Rikarin.Skala.Formatting.CSharp.Arrangement.ArrangementOptions.Implemented) {
                data.Add(OptionRegistry.Get(id).Key);
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ An arrangement option is pinned by a <b>cleanup</b> fixture, not a format-only one.
    /// </summary>
    /// <remarks>
    ///     ⚠ The distinction is the whole of why milestone 4 needed a second oracle profile. A
    ///     format-only fixture is <c>CSReformatCode</c> and nothing else, so it is byte-identical
    ///     whatever the <c>arrange_*</c> keys say; accepting one as evidence for a Tier A arrangement
    ///     claim would be accepting a measurement that cannot come out differently.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ArrangementKeys))]
    public void EveryArrangementOption_IsPinnedByACleanupFixture(string key) {
        Assert.True(OptionRegistry.TryResolve(key, out var id));
        var info = OptionRegistry.Get(id);

        Assert.True(info.Oracle is not null, $"{key} is implemented by the arranger but has no `oracle` glob.");
        var files = Resolve(info.Oracle!);
        Assert.True(files.Count > 0, $"{key}: `oracle` is '{info.Oracle}' and no corpus file matches it.");
        Assert.True(
            files.Any(static file => file.HasFixtureFor(OracleProfile.Cleanup)),
            $"{key}: no committed {OracleProfile.Cleanup.Suffix} beside its corpus file. Run ./build.sh Oracle."
        );
    }

    /// <summary>
    ///     Setting an arrangement option to each of its values must change what the <b>arranger</b>
    ///     produces.
    /// </summary>
    /// <remarks>
    ///     ⚠ The formatter's own version of this test formats the file, which is exactly the wrong
    ///     question for these keys: <c>csharp_style_var_elsewhere</c> changes no whitespace at all and
    ///     would look unimplemented. Same assertion, different subject.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ArrangementKeys))]
    public void EveryArrangementOption_ChangesTheOutputOfItsCorpusFile(string key) {
        Assert.True(OptionRegistry.TryResolve(key, out var id));
        var info = OptionRegistry.Get(id);
        var files = Resolve(info.Oracle!);
        var values = OptionDomain.Probes(info).ToArray();
        Assert.True(values.Length >= 2, $"{key}: fewer than two values to compare.");

        foreach (var file in files) {
            var outputs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values) {
                var resolved = Rikarin.Skala.Core.Configuration.OptionResolver
                    .Resolve(file.Path, [new KeyValuePair<string, string>(key, value)]);

                Assert.True(
                    resolved.ValueErrors.IsEmpty,
                    $"{key} = {value}: {string.Join("; ", resolved.ValueErrors)}"
                );
                outputs.Add(CorpusArranger.RunWith(file, resolved.Options));
            }

            if (outputs.Count > 1) {
                return;
            }
        }

        Assert.Fail(
            $"{key}: setting it to any of [{string.Join(", ", values)}] produces byte-identical arrangement on "
            + $"[{string.Join(", ", files.Select(static f => f.ToString()))}]. An option with no observable effect is "
            + "either unimplemented or wrongly wired; both are bugs."
        );
    }

    [Theory]
    [MemberData(nameof(ImplementedKeys))]
    public void EveryImplementedOption_IsPinnedByACorpusFileWithAnOracleFixture(string key) {
        Assert.True(OptionRegistry.TryResolve(key, out var id));
        var info = OptionRegistry.Get(id);

        Assert.True(info.Oracle is not null, $"{key} is implemented but its registry entry has no `oracle` glob.");
        var files = Resolve(info.Oracle!);
        Assert.True(files.Count > 0, $"{key}: `oracle` is '{info.Oracle}' and no corpus file matches it.");
        Assert.True(
            files.Any(static file => file.HasFixture),
            $"{key}: no committed .expected.cs beside its corpus file. Tier A rests on fixture evidence and nothing else; run ./build.sh Oracle."
        );
    }

    [Theory]
    [MemberData(nameof(ImplementedKeys))]
    public void EveryImplementedOption_ChangesTheOutputOfItsCorpusFile(string key) {
        Assert.True(OptionRegistry.TryResolve(key, out var id));
        var info = OptionRegistry.Get(id);
        var files = Resolve(info.Oracle!);

        var values = OptionDomain.Probes(info).ToArray();
        Assert.True(values.Length >= 2, $"{key}: fewer than two values to compare.");

        foreach (var file in files) {
            var text = CSharpFormatter.Read(file.Path);
            var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var value in values) {
                // ⚠ Flipped from the *repository's* configuration, not from bare registry defaults,
                // and the difference started to matter when M3 replaced the guessed default table
                // with a derived one. An option is observable in the configuration its fixture was
                // generated under; asking the question from ReSharper's own defaults asks a
                // different one, and answers it wrongly whenever a defaulted key masks it —
                // `wrap_after_dot_in_method_calls` cannot be seen while `wrap_chained_method_calls`
                // is at its default of `wrap_if_long`, because nothing chops. That is a true fact
                // about ReSharper's defaults and not a gap in the option's implementation.
                var options = Rikarin.Skala.Core.Configuration.OptionResolver
                    .Resolve(file.Path, [new KeyValuePair<string, string>(key, value)]);

                Assert.True(
                    options.ValueErrors.IsEmpty,
                    $"{key} = {value}: {string.Join("; ", options.ValueErrors)}"
                );

                outputs[value] = CSharpFormatter.Format(file.Path, text, options.Options).Formatted;
            }

            if (outputs.Values.Distinct(StringComparer.Ordinal).Count() > 1) {
                return;
            }
        }

        Assert.Fail(
            $"{key}: setting it to any of [{string.Join(", ", values)}] produces byte-identical output on "
            + $"[{string.Join(", ", files.Select(static f => f.ToString()))}]. An option with no observable effect is "
            + "either unimplemented or wrongly wired; both are bugs."
        );
    }

    static List<CorpusFile> Resolve(string glob) {
        var files = new List<CorpusFile>();
        foreach (var set in new[] { Corpus.Constructs, Corpus.Real, Corpus.Pathological }) {
            var prefix = set + "/";
            if (!glob.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            var pattern = glob[prefix.Length..];
            files.AddRange(Corpus.Files(set).Where(file => Matches(file.RelativePath, pattern)));
        }

        return files;
    }

    static bool Matches(string path, string pattern) {
        if (!pattern.Contains('*', StringComparison.Ordinal)) {
            return string.Equals(path, pattern, StringComparison.Ordinal);
        }

        var parts = pattern.Split('*');
        var cursor = 0;
        for (var i = 0; i < parts.Length; i++) {
            if (parts[i].Length == 0) {
                continue;
            }

            var index = path.IndexOf(parts[i], cursor, StringComparison.Ordinal);
            if (index < 0 || i == 0 && index != 0) {
                return false;
            }

            cursor = index + parts[i].Length;
        }

        return parts[^1].Length == 0 || path.EndsWith(parts[^1], StringComparison.Ordinal);
    }
}
