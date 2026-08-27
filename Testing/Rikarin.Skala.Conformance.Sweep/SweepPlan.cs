using System.Globalization;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>One option the sweep can ask about, with the values to ask and the fixture to ask on.</summary>
/// <param name="Info">The registry entry, read at run time rather than baked in.</param>
/// <param name="Values">The legal values, de-duplicated and in registry order.</param>
/// <param name="Fixture">The corpus file the registry's <c>oracle</c> field names.</param>
public sealed record SweepCandidate(OptionInfo Info, IReadOnlyList<string> Values, CorpusFile Fixture) {
    public string Key => Info.Key;
}

/// <summary>An option the sweep cannot ask about, and why not.</summary>
public sealed record SweepExclusion(OptionInfo Info, string Reason);

/// <summary>What one call to <see cref="SweepPlan.Build" /> produced.</summary>
public sealed record SweepPlanResult(IReadOnlyList<SweepCandidate> Candidates, IReadOnlyList<SweepExclusion> Excluded);

/// <summary>
///     Chooses what the sweep asks about, from the registry as it stands when the sweep runs.
/// </summary>
/// <remarks>
///     ⚠ Nothing here holds a list of option names. Three agents are implementing <c>space_*</c>,
///     <c>wrap_*</c> and <c>xmldoc_*</c> concurrently with this one, so the set of Tier A options moves
///     under the harness; a baked-in list would report against a registry that no longer exists. Every
///     question — which options, which values, which fixture, which tier — is asked of
///     <see cref="OptionRegistry" /> at run time.
///     <para>
///         ⚠ The language filter reads <see cref="OptionInfo.Language" /> and never parses
///         <c>editor_config_template</c>. The export carries C++, VB and XAML keys that no C# fixture can
///         move, and the template is being hand-stripped of them — but ADR-001 requires the full unstripped
///         export to keep working, so the harness must not care either way. The registry's own field is the
///         only stable answer.
///     </para>
/// </remarks>
public static class SweepPlan {
    /// <summary>The languages a C# corpus fixture can speak to.</summary>
    /// <remarks>
    ///     ⚠ An allow-list rather than a deny-list. A key whose language is added to the registry
    ///     tomorrow is excluded and reported, which is the failure mode to prefer over silently
    ///     sweeping a C++ key against a C# fixture and reporting it unexercised.
    /// </remarks>
    public static readonly string[] Languages = ["csharp", "xmldoc", "any"];

    public static SweepPlanResult Build(IReadOnlyList<string> families) {
        var candidates = new List<SweepCandidate>();
        var excluded = new List<SweepExclusion>();
        var arrangement = Rikarin.Skala.Formatting.CSharp.Arrangement.ArrangementOptions.Implemented.ToHashSet();

        foreach (var info in OptionRegistry.All) {
            if (!Languages.Contains(info.Language, StringComparer.Ordinal)) {
                excluded.Add(new SweepExclusion(info, "language is '" + info.Language + "'"));
                continue;
            }

            // ⚠ An arrangement option is pinned by a *cleanup* fixture and read by the *arranger*,
            // and sweeping it here would ask neither question. The format-only profile is
            // `CSReformatCode` and nothing else, so it is byte-identical whatever an `arrange_*` key
            // says — every one of them would come back SPURIOUS, which would be the harness inventing
            // 20 divergences rather than finding any. They need the cleanup profile on the oracle's
            // side and `CorpusArranger` on Skala's; that is a second pass, named in docs/plan/12, not
            // something to approximate with the wrong profile.
            if (arrangement.Contains(info.Id)) {
                excluded.Add(
                    new SweepExclusion(info, "arrangement option: needs the cleanup profile, not CSReformatCode")
                );
                continue;
            }

            if (families.Count > 0 && !InFamily(info.Key, families)) {
                continue;
            }

            if (info.Oracle is not { Length: > 0 } glob) {
                excluded.Add(new SweepExclusion(info, "no `oracle` fixture in the registry"));
                continue;
            }

            var matches = CorpusGlob.Resolve(glob);
            var fixture = matches.Count == 0 ? null : matches[0];
            if (fixture is null) {
                excluded.Add(new SweepExclusion(info, "`oracle` is '" + glob + "' and no corpus file matches it"));
                continue;
            }

            var values = LegalValues(info).Distinct(StringComparer.Ordinal).ToArray();
            if (values.Length < 2) {
                excluded.Add(new SweepExclusion(info, "fewer than two values to compare"));
                continue;
            }

            candidates.Add(new SweepCandidate(info, values, fixture));
        }

        return new SweepPlanResult(candidates, excluded);
    }

    /// <summary>
    ///     <c>--family=space</c> means the <c>space_*</c> keys, whichever prefix the export spells them
    ///     with.
    /// </summary>
    /// <remarks>
    ///     ⚠ The same option is written <c>resharper_space_after_cast</c>, <c>csharp_space_after_cast</c>
    ///     and <c>space_after_cast</c> depending on which of the three the export chose, so a family is
    ///     matched after the vendor prefix is taken off rather than by <c>StartsWith</c> on the raw key.
    /// </remarks>
    public static bool InFamily(string key, IReadOnlyList<string> families) {
        var bare = Strip(key);
        foreach (var family in families) {
            if (bare.StartsWith(family, StringComparison.Ordinal)
                && (bare.Length == family.Length || bare[family.Length] == '_')) {
                return true;
            }
        }

        return false;
    }

    public static string Strip(string key) {
        foreach (var prefix in new[] { "resharper_csharp_", "resharper_xmldoc_", "resharper_", "csharp_", "dotnet_" }) {
            if (key.StartsWith(prefix, StringComparison.Ordinal)) {
                return key[prefix.Length..];
            }
        }

        return key;
    }

    /// <summary>
    ///     The values one option is swept at.
    /// </summary>
    /// <remarks>
    ///     ⚠ An int has no finite domain, so the sweep offers a probe set — the export's value, a value
    ///     that is definitely different, and <c>1</c>, each clamped into the option's declared bounds —
    ///     and not the domain. An int option reported <see cref="SweepOutcome.Unexercised" /> therefore
    ///     means "this probe set could not move it", which is weaker than the same verdict on a bool or
    ///     an enum and is labelled as such in the report.
    ///     <para>
    ///         ⚠ This used to be a copy of <c>OptionCoverageTests.LegalValues</c> with a comment saying
    ///         it was kept identical by hand. There were five such copies; giving int options a minimum
    ///         invalidated four of them at once. <see cref="OptionDomain" /> is the one now.
    ///     </para>
    /// </remarks>
    public static IEnumerable<string> LegalValues(OptionInfo info) => OptionDomain.Probes(info);
}

/// <summary>Resolves a registry <c>oracle</c> glob to the corpus files it names.</summary>
public static class CorpusGlob {
    public static IReadOnlyList<CorpusFile> Resolve(string glob) {
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
            if (index < 0 || (i == 0 && index != 0)) {
                return false;
            }

            cursor = index + parts[i].Length;
        }

        return pattern.EndsWith('*') || cursor == path.Length;
    }
}
