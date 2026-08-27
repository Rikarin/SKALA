namespace Rikarin.Skala.Conformance.Sweep.Tests;

/// <summary>
///     Skala's half of the sweep, measured without an oracle.
/// </summary>
/// <remarks>
///     ⚠ This exists because the harness got it wrong once, in the way that is hardest to notice: Skala's
///     side normalised its output while the oracle's side did not, so
///     <c>resharper_csharp_insert_final_newline</c> came back <c>INERT</c> — "ReSharper honours the key
///     and Skala ignores it" — while <c>skala format --option</c> on the same fixture writes 12 bytes at
///     <c>true</c> and 11 at <c>false</c>. A verdict that damning has to be impossible to reach by
///     accident, and the check costs no oracle run at all: Skala's whole side of a 201-option sweep takes
///     well under a second.
///     <para>
///         It is the same claim <c>OptionCoverageTests.EveryImplementedOption_ChangesTheOutputOfItsCorpusFile</c>
///         makes, asked through the sweep's own code path rather than beside it — which is the only version
///         of it that can catch the sweep normalising one side.
///     </para>
/// </remarks>
public sealed class SkalaSideTests {
    [Fact]
    public void EverySweptOption_MovesSkalasOutputOnItsOwnFixture() {
        var stale = new List<string>();

        foreach (var candidate in SweepPlan.Build([]).Candidates) {
            var outputs = candidate.Values
                .Select(value => KeyFlipSweep.FormatWithSkala(candidate, value))
                .Distinct(StringComparer.Ordinal)
                .Count();

            if (outputs < 2) {
                stale.Add(candidate.Key + " on " + candidate.Fixture);
            }
        }

        Assert.True(
            stale.Count == 0,
            "The sweep's Skala side produces one output across every value of these options, so their "
            + "verdicts can only be INERT or UNEXERCISED. Either the option really is unimplemented — in "
            + "which case OptionCoverageTests should already be red — or the sweep is comparing in the "
            + "wrong units:\n  "
            + string.Join("\n  ", stale)
        );
    }

    /// <summary>⚠ The units are raw bytes, and this is the assertion that says so.</summary>
    /// <remarks>
    ///     <c>resharper_csharp_insert_final_newline</c> changes exactly one byte and nothing that
    ///     survives line-ending normalisation, so it is the canary for the whole class.
    /// </remarks>
    [Fact]
    public void SkalasSide_IsRawBytes_NotNormalisedText() {
        var candidate = SweepPlan.Build([])
            .Candidates
                .Single(static c => c.Key == "resharper_csharp_insert_final_newline");

        var on = KeyFlipSweep.FormatWithSkala(candidate, "true");
        var off = KeyFlipSweep.FormatWithSkala(candidate, "false");

        Assert.NotEqual(on, off);
        Assert.EndsWith("\n", on, StringComparison.Ordinal);
        Assert.False(off.EndsWith('\n'));
    }
}
