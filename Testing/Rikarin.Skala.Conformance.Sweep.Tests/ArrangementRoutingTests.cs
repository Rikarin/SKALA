using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep.Tests;

/// <summary>
///     The 44 arrangement keys, and the routing that lets the sweep ask about them at all.
/// </summary>
/// <remarks>
///     ⚠ <b>What this replaces.</b> Until this file existed, <c>SweepPlan</c> excluded every option in
///     <see cref="ArrangementOptions.Implemented" /> by name, and <c>SweepPlanTests</c> asserted the
///     exclusion. The reason given was true — a <c>CSReformatCode</c> oracle is byte-identical whatever
///     an arrangement key says, so sweeping them under that profile would have reported 44 SPURIOUS rows
///     and called it a finding — but it was a fact about the *profile*, and the profile is a parameter.
///     The consequence was that 15 % of the Tier A claim rested on hand-transcribed flipped-value
///     readings, which is the standard of evidence the key-flip sweep was built to replace.
///     <para>
///         ⚠ Every assertion below fails against the code as it stood before this change, and that is the
///         point of writing them: a test that passes both before and after is not pinning the change.
///     </para>
/// </remarks>
public sealed class ArrangementRoutingTests {
    static IReadOnlyList<OptionInfo> Arrangement =>
        [.. ArrangementOptions.Implemented.Select(static id => OptionRegistry.Get(id))];

    /// <summary>
    ///     ⚠ One authority for "which profile does this fixture want", read by both halves.
    /// </summary>
    /// <remarks>
    ///     The oracle side asks it to choose a <c>cleanupcode</c> profile and Skala's side asks it to
    ///     choose between the formatter and the arrange-and-format pipeline. Two spellings that agreed
    ///     today would be two spellings that could disagree tomorrow, and a disagreement here compares
    ///     an arranged output against a merely formatted one and blames the flipped key.
    /// </remarks>
    [Fact]
    public void BothHalvesOfTheSweep_ChooseTheProfileFromOneAuthority() {
        foreach (var file in Corpus.All()) {
            Assert.Equal(OracleProfile.For(file.Path), ScratchTree.ProfileFor(file));
        }
    }

    /// <summary>The arrangement subtree is the cleanup profile's, and nothing else in the corpus is.</summary>
    [Fact]
    public void TheArrangementSubtree_IsRoutedToTheCleanupProfile() {
        var routed = 0;
        foreach (var file in Corpus.All()) {
            var expected = file.Set == Corpus.Constructs
                && file.RelativePath.StartsWith(Corpus.ArrangementPrefix, StringComparison.Ordinal);

            Assert.Equal(expected, ScratchTree.ProfileFor(file) == OracleProfile.Cleanup);
            if (expected) {
                routed++;
            }
        }

        // ⚠ The population canary. Every assertion above is satisfied by a corpus with no arrangement
        // files in it, which is the one reading that means this test asserted nothing.
        Assert.True(routed > 0, "no corpus file routes to the cleanup profile, so this test asserted nothing.");
    }

    /// <summary>
    ///     ⚠ All 44 are in the plan, and the exclusion that refused them is gone.
    /// </summary>
    /// <remarks>
    ///     The count is asserted as "every implemented arrangement option" rather than as the literal 44,
    ///     because the registry moves under this harness and a baked-in number would be a second
    ///     statement of the same fact that could drift from the first.
    /// </remarks>
    [Fact]
    public void EveryArrangementOption_IsNowSwept() {
        var plan = SweepPlan.Build([]);
        var swept = plan.Candidates.Select(static candidate => candidate.Info.Id).ToHashSet();
        var missing = new List<string>();

        foreach (var info in Arrangement) {
            if (!swept.Contains(info.Id)) {
                var reason = plan.Excluded.FirstOrDefault(exclusion => exclusion.Info.Id == info.Id)?.Reason
                    ?? "not in the plan at all, and not excluded either";
                missing.Add(info.Key + ": " + reason);
            }
        }

        Assert.True(
            missing.Count == 0,
            "these arrangement options are still not measured by the sweep:\n  " + string.Join("\n  ", missing)
        );

        Assert.NotEmpty(Arrangement);
    }

    /// <summary>
    ///     ⚠ And every one of them is measured under a profile that can answer.
    /// </summary>
    /// <remarks>
    ///     An arrangement key whose <c>oracle</c> glob named a fixture outside
    ///     <c>constructs/arrangement/</c> would be run <c>CSReformatCode</c> on both sides — the old
    ///     exclusion's reasoning, still correct in that narrow case. <c>SweepPlan</c> keeps exactly that
    ///     much of it, so a swept arrangement candidate always carries a semantic fixture.
    /// </remarks>
    [Fact]
    public void EverySweptArrangementOption_HasAFixtureTheCleanupProfileOwns() {
        var implemented = ArrangementOptions.Implemented.ToHashSet();

        foreach (var candidate in SweepPlan.Build([]).Candidates) {
            if (!implemented.Contains(candidate.Info.Id)) {
                continue;
            }

            Assert.True(
                ScratchTree.ProfileFor(candidate.Fixture).IsSemantic,
                candidate.Key
                + " is swept against "
                + candidate.Fixture
                + ", which the format-only profile owns. The oracle cannot arrange it, so the verdict "
                + "would be SPURIOUS about the profile rather than about the key."
            );
        }
    }

    /// <summary>
    ///     ⚠ Skala's half of a cleanup comparison is the arranger, not the formatter.
    /// </summary>
    /// <remarks>
    ///     The check is that <c>SkalaSide</c> and <c>CSharpFormatter</c> <em>differ</em> on an
    ///     arrangement fixture: a routing that silently fell back to formatting would leave the sweep
    ///     comparing a formatted file against an arranged one and reporting every arrangement key
    ///     DIVERGENT on a difference no key caused.
    /// </remarks>
    [Fact]
    public void SkalasSide_ArrangesACleanupFixture_RatherThanOnlyFormattingIt() {
        var candidate = SweepPlan.Build([])
            .Candidates
                .Single(static c => c.Key == "dotnet_style_qualification_for_field");

        var path = candidate.Fixture.Path;
        var arranged = SkalaSide.Format(path, candidate.Key, "true");
        var formatted = CSharpFormatter.Format(
            path,
            CSharpFormatter.Read(path),
            Rikarin.Skala.Core.Configuration.OptionResolver
                .Resolve(path, [new KeyValuePair<string, string>(candidate.Key, "true")])
                .Options
        ).Formatted;

        Assert.NotEqual(formatted, arranged);
        Assert.DoesNotContain("did-not-converge", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A semantic batch never holds one fixture twice.
    /// </summary>
    /// <remarks>
    ///     44 arrangement keys point at 22 fixtures, so four of them name
    ///     <c>redundancy/qualifiers-and-parentheses.cs</c>. A count-batched round would copy that one
    ///     file into four directories of one scratch project — four declarations of one type in one
    ///     namespace — and every semantic rewrite in the cleanup profile would then be reading a
    ///     compilation full of CS0101. The verdicts would be a measurement of the scratch directory.
    /// </remarks>
    [Fact]
    public void ASemanticBatch_NeverHoldsOneFixtureTwice() {
        var implemented = ArrangementOptions.Implemented.ToHashSet();
        var candidates = SweepPlan.Build([])
            .Candidates
                .Where(candidate => implemented.Contains(candidate.Info.Id))
                .ToArray();

        var batches = ScratchTree.Batches(
            candidates,
            static candidate => candidate.Fixture,
            OracleProfile.Cleanup,
            KeyFlipSweep.BatchSize
        )
            .ToArray();

        var shared = candidates.GroupBy(static candidate => candidate.Fixture.Path, StringComparer.Ordinal)
            .Max(static group => group.Count());

        // ⚠ Without this the assertion below is vacuous: a candidate set in which no two keys share a
        // fixture is cut into one batch by either rule, and the test would pass against the code it
        // was written to refuse.
        Assert.True(shared > 1, "no two swept arrangement keys share a fixture, so this test asserted nothing.");

        foreach (var batch in batches) {
            var paths = batch.Select(static candidate => candidate.Fixture.Path).ToArray();
            Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
        }

        // Nothing is dropped and nothing is answered twice.
        Assert.Equal(
            candidates.Select(static candidate => candidate.Key).OrderBy(static key => key, StringComparer.Ordinal),
            batches.SelectMany(static batch => batch)
                .Select(static candidate => candidate.Key)
                .OrderBy(static key => key, StringComparer.Ordinal)
        );

        Assert.Equal(shared, batches.Length);
    }

    /// <summary>
    ///     ⚠ And a whitespace batch is still cut by count alone.
    /// </summary>
    /// <remarks>
    ///     The 855-configuration format-only sweep is affordable because a batch is 60 fixtures and one
    ///     <c>cleanupcode</c> startup. Applying the semantic rule to it would multiply its invocations
    ///     to re-answer a question <c>CSReformatCode</c> cannot be affected by: two copies of a file
    ///     cannot change the whitespace either of them is given.
    /// </remarks>
    [Fact]
    public void AWhitespaceBatch_IsStillCutByCountAlone() {
        var candidates = SweepPlan.Build([]).Candidates.Take(KeyFlipSweep.BatchSize + 5).ToArray();
        var batches = ScratchTree.Batches(
            candidates,
            static candidate => candidate.Fixture,
            OracleProfile.FormatOnly,
            KeyFlipSweep.BatchSize
        )
            .ToArray();

        Assert.Equal(2, batches.Length);
        Assert.Equal(KeyFlipSweep.BatchSize, batches[0].Count);
        Assert.Equal(5, batches[1].Count);
    }

    /// <summary>
    ///     ⚠ The two canaries can see an arrangement-only failure.
    /// </summary>
    /// <remarks>
    ///     A round now holds three populations answered by three <c>cleanupcode</c> profiles. Pooled
    ///     across the round, 44 arrangement options that answered nothing sit inside 378 whose
    ///     whitespace half moved normally — so <c>moved &gt; 0</c>, the canary stays silent, and the
    ///     table reports 44 rows of universal agreement about a profile that never ran. The counts are
    ///     therefore taken per profile, and these are the numbers that would be handed to the
    ///     predicates in that case.
    /// </remarks>
    [Fact]
    public void TheCanariesFire_OnAnArrangementOnlyFailure() {
        var arrangement = Arrangement.Count;
        Assert.True(arrangement > 1, "there are no arrangement options, so this test asserted nothing.");

        // The oracle errored on the whole cleanup partition, while the whitespace partition was fine.
        Assert.True(KeyFlipSweep.IsBrokenMeasurement(arrangement, 0));

        // The oracle answered every arrangement option with the file it was given: the configurations
        // are reaching it and not varying, which is what a wrong profile looks like from outside.
        Assert.True(KeyFlipSweep.IsUnvaryingRound(arrangement, 0));

        // ⚠ And the pooled counts a round-wide canary would have been handed instead. Both are silent,
        // which is the reading this per-profile split exists to prevent.
        var round = SweepPlan.Build([]).Candidates.Count;
        Assert.False(KeyFlipSweep.IsBrokenMeasurement(round, round - arrangement));
        Assert.False(KeyFlipSweep.IsUnvaryingRound(round, round - arrangement));
    }
}
