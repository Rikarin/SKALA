using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep.Tests;

/// <summary>What the sweep chooses to ask about, and what it refuses to.</summary>
public sealed class SweepPlanTests {
    /// <summary>
    ///     ⚠ Selection is by the registry's <c>language</c> field, never by parsing the export.
    /// </summary>
    /// <remarks>
    ///     The C++/VB/XAML keys are being stripped out of <c>editor_config_template</c> by hand, so that
    ///     file is about to change — and ADR-001 requires the full unstripped export to keep working
    ///     regardless. A harness that decided what to sweep by reading the template would answer
    ///     differently before and after the strip, for no reason connected to the options.
    /// </remarks>
    [Fact]
    public void EverySweptOption_HasALanguageTheCorpusCanSpeakTo() {
        var plan = SweepPlan.Build([]);

        Assert.NotEmpty(plan.Candidates);
        foreach (var candidate in plan.Candidates) {
            Assert.Contains(candidate.Info.Language, SweepPlan.Languages, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void EverySweptOption_HasAFixtureAndAtLeastTwoValues() {
        foreach (var candidate in SweepPlan.Build([]).Candidates) {
            Assert.True(candidate.Values.Count >= 2, candidate.Key + " has fewer than two values");
            Assert.True(File.Exists(candidate.Fixture.Path), candidate.Key + " names a fixture that is not there");
        }
    }

    /// <summary>
    ///     ⚠ An arrangement option is excluded rather than swept under the wrong profile.
    /// </summary>
    /// <remarks>
    ///     The format-only profile is <c>CSReformatCode</c> and nothing else, so its output is
    ///     byte-identical whatever an <c>arrange_*</c> key says. Sweeping them here would report every
    ///     one as <c>SPURIOUS</c> — the harness inventing divergences rather than finding any.
    /// </remarks>
    [Fact]
    public void ArrangementOptions_AreExcludedWithTheirReasonRecorded() {
        var plan = SweepPlan.Build([]);
        var swept = plan.Candidates.Select(static candidate => candidate.Info.Id).ToHashSet();

        foreach (var id in Rikarin.Skala.Formatting.CSharp.Arrangement.ArrangementOptions.Implemented) {
            Assert.DoesNotContain(id, swept);
            Assert.Contains(
                plan.Excluded,
                exclusion => exclusion.Info.Id == id
                    && exclusion.Reason.Contains("arrangement", StringComparison.Ordinal)
            );
        }
    }

    /// <summary>A family is matched after the vendor prefix, because the export spells keys three ways.</summary>
    [Theory]
    [InlineData("resharper_csharp_space_after_cast", "space", true)]
    [InlineData("csharp_space_after_cast", "space", true)]
    [InlineData("space_after_cast", "space", true)]
    [InlineData("resharper_wrap_before_comma", "wrap", true)]
    [InlineData("resharper_csharp_space_after_cast", "wrap", false)]
    // ⚠ A prefix match on the bare name is not enough: `spaces_around` would claim `space` and
    // `blank_lines` would claim `blank_line`. The family has to end on an underscore boundary.
    [InlineData("resharper_spaces_within", "space", false)]
    [InlineData("resharper_wrapping_style", "wrap", false)]
    public void InFamily_MatchesOnAnUnderscoreBoundary(string key, string family, bool expected) =>
        Assert.Equal(expected, SweepPlan.InFamily(key, [family]));

    /// <summary>
    ///     The sweep's value set is the same one the option unit floor uses.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>OptionCoverageTests</c> asserts Skala's output moves across these values and the sweep
    ///     asserts ReSharper's does too. If the two drew from different value sets, a disagreement
    ///     between them would be a disagreement about the question rather than about the answer.
    /// </remarks>
    [Fact]
    public void EnumOptions_AreSweptOverTheirWholeDomain() {
        foreach (var info in OptionRegistry.All.Where(static info => info.Kind == OptionValueKind.Enum)) {
            var values = SweepPlan.LegalValues(info).ToArray();
            Assert.Equal(OptionEnums.ValuesOf(info.EnumName!).ToArray().Length, values.Length);
        }
    }

    /// <summary>
    ///     ⚠ The spelling the sweep appends must be the most specific one the export uses.
    /// </summary>
    /// <remarks>
    ///     The oracle side of the sweep forces a key by appending <c>[*.cs] &lt;key&gt; = &lt;value&gt;</c>
    ///     to a copy of the export. Appending wins on order — but ReSharper also ranks *spellings*, and
    ///     <c>resharper_csharp_x</c> outranks <c>resharper_x</c> whichever comes last. So if the export
    ///     wrote a more specific spelling of an option than the canonical key the sweep appends, the
    ///     override would be silently ignored, the oracle would produce one output at every value, and
    ///     the option would be reported <c>SPURIOUS</c> — a divergence manufactured by the harness.
    ///     <para>
    ///         This holds today for every swept option, which is what makes the <c>SPURIOUS</c> rows real.
    ///         It is a fact about the export, and the export is re-generated from Rider whenever a setting
    ///         changes, so it is asserted rather than assumed.
    ///     </para>
    /// </remarks>
    [Fact]
    public void NoSweptOption_HasAMoreSpecificSpellingInTheExport() {
        var assigned = Assignments();

        foreach (var candidate in SweepPlan.Build([]).Candidates) {
            var canonical = OptionResolver.SpecificityOf(candidate.Key);
            var outranking = candidate.Info.Aliases
                .Where(alias => assigned.Contains(alias) && OptionResolver.SpecificityOf(alias) < canonical)
                .ToArray();

            Assert.True(
                outranking.Length == 0,
                candidate.Key
                + ": the export also writes "
                + string.Join(", ", outranking)
                + ", which outranks the spelling the sweep appends. Its oracle side would be inert."
            );
        }
    }

    static HashSet<string> Assignments() {
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(Path.Combine(Corpus.RepositoryRoot, ".editorconfig"))) {
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0 || line.AsSpan().TrimStart() is ['#', ..] or ['[', ..]) {
                continue;
            }

            assigned.Add(line[..equals].Trim());
        }

        return assigned;
    }

    [Fact]
    public void BoolOptions_AreSweptOverBothValues() {
        foreach (var info in OptionRegistry.All.Where(static info => info.Kind == OptionValueKind.Bool)) {
            Assert.Equal(["true", "false"], SweepPlan.LegalValues(info));
        }
    }
}
