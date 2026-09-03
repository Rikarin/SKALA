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

    // ⚠ `ArrangementOptions_AreExcludedWithTheirReasonRecorded` stood here and asserted the opposite
    // of what `ArrangementRoutingTests.EveryArrangementOption_IsNowSwept` asserts. It was right about
    // the profile and wrong about the option: `CSReformatCode` is byte-identical whatever an
    // arrangement key says, so sweeping all 44 under it would have reported 44 SPURIOUS rows — but
    // the profile is a parameter, and the fixture now chooses it. The narrow half of its reasoning
    // that survives is pinned by
    // `ArrangementRoutingTests.EverySweptArrangementOption_HasAFixtureTheCleanupProfileOwns`.

    /// <summary>
    ///     A family is matched after the prefix, because a key carries one of several.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two of these cases used to be <c>resharper_csharp_space_after_cast</c> and
    ///     <c>csharp_space_after_cast</c>, and the <c>skala_</c> rename collapsed them onto one
    ///     spelling — leaving a duplicate <c>InlineData</c> that asserted the same thing twice and a
    ///     theory that no longer covered a second prefix at all. The point of the case is that
    ///     <see cref="SweepPlan.Strip" /> handles <em>every</em> prefix
    ///     <see cref="OptionKeyPrefixes.Ordered" /> lists, so the cases are one per prefix, and
    ///     <see cref="EveryKeyPrefix_IsMatchedAfterItsPrefix" /> fails if a prefix is added without
    ///     one.
    /// </remarks>
    [Theory]
    [InlineData("skala_space_after_cast", "space", true)]
    [InlineData("skala_xmldoc_wrap_lines", "wrap", true)]
    [InlineData("csharp_space_after_dot", "space", true)]
    [InlineData("dotnet_style_qualification_for_field", "style", true)]
    [InlineData("skala_wrap_before_comma", "wrap", true)]
    [InlineData("skala_space_after_cast", "wrap", false)]
    // ⚠ A prefix match on the bare name is not enough: `spaces_around` would claim `space` and
    // `blank_lines` would claim `blank_line`. The family has to end on an underscore boundary.
    [InlineData("skala_spaces_within", "space", false)]
    [InlineData("skala_wrapping_style", "wrap", false)]
    public void InFamily_MatchesOnAnUnderscoreBoundary(string key, string family, bool expected) =>
        Assert.Equal(expected, SweepPlan.InFamily(key, [family]));

    /// <summary>
    ///     Every prefix the generator emits is stripped before a family is matched.
    /// </summary>
    /// <remarks>
    ///     ⚠ The instrument check for the theory above. A prefix added to
    ///     <see cref="OptionKeyPrefixes.Ordered" /> and not to <see cref="SweepPlan.Strip" /> makes
    ///     <c>--family=</c> silently skip every option carrying it — the run succeeds and reports
    ///     fewer rows, which reads as "nothing to measure" rather than as a fault.
    /// </remarks>
    [Fact]
    public void EveryKeyPrefix_IsMatchedAfterItsPrefix() {
        Assert.NotEmpty(OptionKeyPrefixes.Ordered);
        foreach (var prefix in OptionKeyPrefixes.Ordered) {
            Assert.Equal(string.Empty, SweepPlan.Strip(prefix));
            Assert.True(SweepPlan.InFamily(prefix + "space_after_cast", ["space"]), prefix);
        }
    }

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
        foreach (var line in File.ReadAllLines(Corpus.OracleEditorConfigPath)) {
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
