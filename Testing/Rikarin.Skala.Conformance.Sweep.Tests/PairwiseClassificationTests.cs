namespace Rikarin.Skala.Conformance.Sweep.Tests;

/// <summary>
///     The pairwise verdict, and the reachability predicate the whole pass rests on.
/// </summary>
/// <remarks>
///     ⚠ <c>INTERACTION</c> is the only product of this pass that the one-at-a-time sweep cannot also
///     produce, and it is derived entirely from <see cref="PairwiseSweep.ReachedBySingleSweep" />. Get
///     that predicate backwards and every genuine interaction is filed as an ordinary divergence — where
///     it looks like a duplicate of a row `conformance-sweep.md` already has, and gets closed as one.
///     Nothing downstream would notice.
/// </remarks>
public sealed class PairwiseClassificationTests {
    /// <summary>
    ///     ⚠ Reachability turns on the <b>secondary</b> alone, and the first version turned on either.
    /// </summary>
    /// <remarks>
    ///     The tempting reading is "reachable when either key sits at the export's value", because the
    ///     single sweep flips one key and leaves the rest at the export's. That covers the grid's whole
    ///     cross — but only across the two <em>fixtures</em> involved. On the primary's fixture the single
    ///     sweep measures the primary's values against the secondary's export value and nothing else; the
    ///     column is measured on the secondary's own fixture and says nothing about this one.
    ///     <para>
    ///         ⚠ The first pairwise run classified 58 disagreeing corners at (primary at export, secondary
    ///         moved) as reachable, reported zero interactions, and the zero was an artefact of this line.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ReachabilityTurnsOnTheSecondaryAlone() {
        // The secondary at the export's value: this is exactly a row of the single sweep's
        // measurement of the primary, on the primary's own fixture.
        Assert.True(PairwiseSweep.ReachedBySingleSweep("true", "true"));

        // ⚠ The secondary moved. Nothing has measured this on this fixture, whatever the primary is.
        Assert.False(PairwiseSweep.ReachedBySingleSweep("true", "false"));

        // ⚠ Half a bool × bool grid is interior, not a quarter of it.
        Assert.False(PairwiseSweep.ReachedBySingleSweep("false", "true"));
        Assert.True(PairwiseSweep.ReachedBySingleSweep("false", "false"));
    }

    /// <summary>⚠ A secondary with no recorded default makes every corner interior, and must not crash.</summary>
    [Fact]
    public void AnUnknownDefault_MakesNoCornerReachable() =>
        Assert.False(PairwiseSweep.ReachedBySingleSweep(null, "true"));

    /// <summary>
    ///     ⚠ A fixture the two engines already disagree on cannot report on a pair.
    /// </summary>
    /// <remarks>
    ///     Measured: the first pairwise run had 17 disagreeing corners sitting at the base configuration
    ///     itself — both keys at the export's value, nothing set. Those pairs were reported
    ///     <c>DIVERGENT</c>, which blames a pair for a divergence its fixture had before either key was
    ///     touched, and 43 such rows would bury the handful this pass exists to find.
    /// </remarks>
    [Fact]
    public void AFixtureThatAlreadyDiverges_IsNotAFindingAboutThePair() =>
        Assert.Equal(
            PairOutcome.BaselineDivergent,
            PairSweep.Classify(
                oracleDistinct: 2,
                skalaDistinct: 2,
                corners: [Corner("true", "true", false, true)],
                baselineAgrees: false
            )
        );

    [Fact]
    public void EveryCornerAgreeing_IsConformant() =>
        Assert.Equal(
            PairOutcome.Conformant,
            PairSweep.Classify(
                oracleDistinct: 2,
                skalaDistinct: 2,
                corners: [
                    Corner("true", "true", true, true),
                    Corner("false", "false", true, false)
                ],
                baselineAgrees: true
            )
        );

    /// <summary>⚠ The verdict this pass exists to produce.</summary>
    [Fact]
    public void OnlyTheInteriorDisagreeing_IsInteractionOnly() =>
        Assert.Equal(
            PairOutcome.InteractionOnly,
            PairSweep.Classify(
                oracleDistinct: 2,
                skalaDistinct: 2,
                corners: [
                    Corner("true", "true", true, true),
                    Corner("false", "true", true, true),
                    Corner("true", "false", false, false),
                    Corner("false", "false", true, false)
                ],
                baselineAgrees: true
            )
        );

    /// <summary>
    ///     ⚠ A disagreement the single sweep could also see is an ordinary divergence, even when an
    ///     interior corner disagrees too.
    /// </summary>
    /// <remarks>
    ///     Otherwise a pair that is simply broken everywhere would be filed as an interaction and sent
    ///     to whoever is looking for subtle two-key defects, when the single sweep's row already says
    ///     the option is wrong on its own.
    /// </remarks>
    [Fact]
    public void ADisagreementInsideTheCross_IsOrdinaryDivergence() =>
        Assert.Equal(
            PairOutcome.Divergent,
            PairSweep.Classify(
                oracleDistinct: 2,
                skalaDistinct: 2,
                corners: [
                    Corner("true", "true", false, true),
                    Corner("false", "false", false, false)
                ],
                baselineAgrees: true
            )
        );

    /// <summary>
    ///     ⚠ Neither engine moved across the whole grid: not a pass, however many corners "agreed".
    /// </summary>
    /// <remarks>
    ///     The same trap as <c>OptionSweep.Classify</c>'s, one dimension up and rather easier to fall
    ///     into: a four-corner grid of identical outputs has four agreements, which reads as the most
    ///     conformant row in the table.
    /// </remarks>
    [Fact]
    public void NeitherEngineMoved_IsUnexercised_AndNeverConformant() {
        var outcome = PairSweep.Classify(
            oracleDistinct: 1,
            skalaDistinct: 1,
            corners: [
                Corner("true", "true", true, true),
                Corner("false", "true", true, true),
                Corner("true", "false", true, false),
                Corner("false", "false", true, false)
            ],
            baselineAgrees: true
        );

        Assert.Equal(PairOutcome.Unexercised, outcome);
        Assert.NotEqual(PairOutcome.Conformant, outcome);
    }

    /// <summary>⚠ The grid is row-major and stable, because the committed table is read as a diff.</summary>
    [Fact]
    public void TheGridIsRowMajorOverPrimaryThenSecondary() {
        var candidate = new PairCandidate(
            Info("resharper_csharp_keep_existing_enum_arrangement"),
            Info("resharper_keep_user_linebreaks"),
            ["true", "false"],
            ["alpha", "beta"],
            new Rikarin.Skala.Testing.CorpusFile("constructs", "sample.cs", "/nowhere/sample.cs")
        );

        Assert.Equal(4, candidate.Corners);
        Assert.Equal(["true", "alpha"], PairwiseSweep.Overrides(candidate, 0).Select(static o => o.Value));
        Assert.Equal(["true", "beta"], PairwiseSweep.Overrides(candidate, 1).Select(static o => o.Value));
        Assert.Equal(["false", "alpha"], PairwiseSweep.Overrides(candidate, 2).Select(static o => o.Value));
        Assert.Equal(["false", "beta"], PairwiseSweep.Overrides(candidate, 3).Select(static o => o.Value));
    }

    /// <summary>
    ///     ⚠ A disagreement one of the keys already owns alone is not an interaction.
    /// </summary>
    /// <remarks>
    ///     Measured: the corrected first run reported 17 <c>INTERACTION</c> rows across <c>wrap_*</c>,
    ///     every one disagreeing only where <c>max_line_length</c> was <c>0</c> or <c>1</c> — the two
    ///     values at which <c>conformance-sweep.json</c> records <c>max_line_length</c> disagreeing
    ///     measured alone on its own fixture. Seventeen findings, one cause, none about a pair. Without
    ///     this rule the pass's headline number is noise.
    /// </remarks>
    [Fact]
    public void ADisagreementOneKeyAlreadyOwns_IsInheritedAndNotAnInteraction() =>
        Assert.Equal(
            PairOutcome.Inherited,
            PairSweep.Classify(
                oracleDistinct: 2,
                skalaDistinct: 2,
                corners: [
                    Corner("true", "120", true, true),
                    Corner("true", "1", false, false, true),
                    Corner("false", "1", false, false, true)
                ],
                baselineAgrees: true
            )
        );

    /// <summary>
    ///     ⚠ One unattributable disagreement is enough to keep the row a finding.
    /// </summary>
    /// <remarks>
    ///     The excuse is per corner and the verdict needs <em>every</em> disagreement excused. A rule that
    ///     excused the row as soon as any corner was attributable would hide a real interaction behind an
    ///     unrelated known divergence, which is the failure this whole verdict is meant to prevent
    ///     happening in the other direction.
    /// </remarks>
    [Fact]
    public void OneUnattributableDisagreement_KeepsTheRowAnInteraction() =>
        Assert.Equal(
            PairOutcome.InteractionOnly,
            PairSweep.Classify(
                oracleDistinct: 2,
                skalaDistinct: 2,
                corners: [
                    Corner("true", "120", true, true),
                    Corner("true", "1", false, false, true),
                    Corner("false", "1", false, false)
                ],
                baselineAgrees: true
            )
        );

    static Rikarin.Skala.Options.OptionInfo Info(string key) {
        Assert.True(Rikarin.Skala.Options.OptionRegistry.TryResolve(key, out var id), key + " is not in options.json");
        return Rikarin.Skala.Options.OptionRegistry.Get(id);
    }

    static PairCorner Corner(
        string primary,
        string secondary,
        bool agree,
        bool reached,
        bool attributable = false
    ) =>
        new(primary, secondary, "aaaaaaaa", agree ? "aaaaaaaa" : "bbbbbbbb", agree, reached, attributable);
}
