using Rikarin.Skala.Conformance.Sweep;

namespace Rikarin.Skala.Conformance.Sweep.Tests;

/// <summary>
/// The three-way verdict, pinned as a truth table.
/// </summary>
/// <remarks>
/// ⚠ This is the part of the harness most likely to be got wrong, and getting it wrong rebuilds the
/// exact defect the harness exists to detect. Skala reached 99.70 % fidelity while respecting 205 of
/// the 458 keys the export sets, and flipping <c>resharper_int_align</c> produced byte-identical
/// output that no test noticed — both because a measurement at one configuration cannot tell
/// "honoured" from "happens to agree". A classifier that answers <c>Conformant</c> when neither
/// engine moved reproduces that hole inside the instrument built to find it.
/// </remarks>
public sealed class SweepClassificationTests {
    [Fact]
    public void NeitherEngineMoved_IsUnexercised_AndNeverConformant() {
        // Both engines produced one distinct output across two values, and those outputs agreed at
        // both. That is the shape most likely to be mistaken for a pass: every value agrees.
        var outcome = OptionSweep.Classify(oracleDistinct: 1, skalaDistinct: 1, agreements: 2, values: 2);

        Assert.Equal(SweepOutcome.Unexercised, outcome);
        Assert.NotEqual(SweepOutcome.Conformant, outcome);
    }

    [Fact]
    public void BothMoved_AndEveryValueAgrees_IsConformant() =>
        Assert.Equal(
            SweepOutcome.Conformant,
            OptionSweep.Classify(oracleDistinct: 2, skalaDistinct: 2, agreements: 2, values: 2)
        );

    [Fact]
    public void BothMoved_AndSomeValueDisagrees_IsDivergent() =>
        Assert.Equal(
            SweepOutcome.Divergent,
            OptionSweep.Classify(oracleDistinct: 2, skalaDistinct: 2, agreements: 1, values: 2)
        );

    /// <summary>⚠ <c>resharper_int_align</c>: ReSharper honours the key and Skala ignores it.</summary>
    [Fact]
    public void OracleMoved_AndSkalaDidNot_IsInert() =>
        Assert.Equal(
            SweepOutcome.Inert,
            OptionSweep.Classify(oracleDistinct: 2, skalaDistinct: 1, agreements: 1, values: 2)
        );

    [Fact]
    public void SkalaMoved_AndTheOracleDidNot_IsSpurious() =>
        Assert.Equal(
            SweepOutcome.Spurious,
            OptionSweep.Classify(oracleDistinct: 1, skalaDistinct: 2, agreements: 1, values: 2)
        );

    /// <summary>
    /// ⚠ One engine moving is a divergence even when every value happened to agree.
    /// </summary>
    /// <remarks>
    /// The combination is only reachable when an output is missing on one side, and scoring it
    /// <c>Conformant</c> would let a <c>cleanupcode</c> run that skipped a file read as a pass. The
    /// agreement count may not override the fact that the two engines disagree about whether the
    /// option does anything.
    /// </remarks>
    [Fact]
    public void OneEngineMoving_IsNeverConformant_HoweverManyValuesAgreed() {
        Assert.NotEqual(
            SweepOutcome.Conformant,
            OptionSweep.Classify(oracleDistinct: 2, skalaDistinct: 1, agreements: 2, values: 2)
        );
        Assert.NotEqual(
            SweepOutcome.Conformant,
            OptionSweep.Classify(oracleDistinct: 1, skalaDistinct: 2, agreements: 2, values: 2)
        );
    }

    [Fact]
    public void OnlyConformant_CountsAsGreen() {
        foreach (var outcome in Enum.GetValues<SweepOutcome>()) {
            var option = Sample(outcome);
            Assert.Equal(outcome == SweepOutcome.Conformant, option.IsGreen);
        }
    }

    /// <summary>
    /// ⚠ The broken-measurement canary, pinned because a healthy run cannot demonstrate it.
    /// </summary>
    /// <remarks>
    /// Both of this harness's confident-wrong-verdict bugs had the shape "a non-empty population in
    /// which nothing was observed": M3's "197 options set, 0 fixtures unchanged" and this harness's
    /// own "0/164 fixtures agree at the baseline". Neither errored; both printed a table. The canary
    /// is silent on every correct run — which is precisely why its firing has to be pinned here
    /// rather than trusted to be observed in the sweep's own output.
    /// </remarks>
    [Fact]
    public void TheBrokenMeasurementCanary_FiresOnlyWhenNothingWasObservedAtAll() {
        // The two shapes that have actually happened.
        Assert.True(KeyFlipSweep.IsBrokenMeasurement(population: 164, observed: 0));
        Assert.True(KeyFlipSweep.IsBrokenMeasurement(population: 197, observed: 0));

        // ⚠ And it must stay silent on a healthy run, or it is noise that gets ignored. These are
        // this sweep's own real counts: 199 of 207 fixtures agreed at the baseline, and 180 of 258
        // oracle outputs moved in round 1.
        Assert.False(KeyFlipSweep.IsBrokenMeasurement(population: 207, observed: 199));
        Assert.False(KeyFlipSweep.IsBrokenMeasurement(population: 258, observed: 180));

        // A single observation is enough to say the instrument reached the subject.
        Assert.False(KeyFlipSweep.IsBrokenMeasurement(population: 258, observed: 1));

        // ⚠ An empty population is not a broken measurement, it is nothing to measure — a
        // `--family` filter that matched no option must not print a defect report.
        Assert.False(KeyFlipSweep.IsBrokenMeasurement(population: 0, observed: 0));
    }

    static OptionSweep Sample(SweepOutcome outcome) =>
        new(
            "resharper_sample",
            Rikarin.Skala.Options.OptionTier.A,
            Rikarin.Skala.Options.OptionValueKind.Bool,
            "constructs/sample.cs",
            outcome,
            [new SweepValue("true", "a", "a", true), new SweepValue("false", "b", "b", true)],
            OracleDistinct: 2,
            SkalaDistinct: 2,
            BaselineAgrees: true,
            LineEndingOnly: false,
            Cost: TimeSpan.Zero
        );
}
