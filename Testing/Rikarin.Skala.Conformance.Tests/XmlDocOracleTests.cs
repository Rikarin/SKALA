using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;
using System.Globalization;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     The documentation-comment family, measured against an oracle profile that formats
///     documentation comments.
/// </summary>
/// <remarks>
///     ⚠ For six milestones this suite could not exist, and the reason it could not was wrong.
///     SK-DIV-0006 recorded that every committed <c>.expected.cs</c> returns its documentation
///     comments exactly as written, and read that as "the oracle declines to format documentation
///     comments"; 22 keys were held at Tier D on the strength of it. The silence was a property of
///     <see cref="OracleProfile.FormatOnly" />, which is byte-for-byte ReSharper's
///     <c>Built-in: Reformat Code</c> — the one built-in profile with <c>CSharpFormatDocComments</c>
///     switched off. <see cref="OracleProfile.DocComments" /> switches it on, and
///     <c>constructs/xmldoc/</c> carries one corpus file per key with the oracle's answer beside it.
///     <para>
///         ⚠ The interesting assertion is the second one, and it runs in both directions. A key the
///         registry calls Tier A must reproduce its fixture byte for byte, which is the ordinary Tier A
///         claim. A key the registry calls Tier D must <em>not</em> — because Tier D here no longer
///         means "unmeasurable", it means "measured, and disagreeing", and a disagreement that quietly
///         went away is a promotion nobody made. That is the same shape as
///         <c>OptionObservabilityTests.AnInertKey_StillCannotBeObserved</c>, for the same reason: a
///         reason nothing checks is a reason that rots.
///     </para>
/// </remarks>
public sealed class XmlDocOracleTests {
    public static TheoryData<string> Files {
        get {
            var data = new TheoryData<string>();
            foreach (var file in Corpus.DocCommented()) {
                data.Add(file.RelativePath);
            }

            return data;
        }
    }

    [Fact]
    public void TheDocCommentedSubtree_IsNotEmpty() {
        // ⚠ A theory over an empty set passes, which is how a corpus subtree that stopped being
        // enumerated takes a whole suite with it and reports success.
        Assert.NotEmpty(Corpus.DocCommented());
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryDocCommentedFile_IsNamedAfterARegistryKey(string relativePath) {
        var key = Path.GetFileNameWithoutExtension(relativePath);
        Assert.True(
            OptionRegistry.TryResolve(key, out _),
            $"constructs/xmldoc/{relativePath}: the subtree is one file per option key and '{key}' is not one. "
            + "The verdict below keys off the file name, so a file named anything else is a file whose "
            + "measurement is attributed to nothing."
        );
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryDocCommentedFile_HasACommittedDocCommentFixture(string relativePath) {
        var file = Corpus.DocCommented().Single(candidate => candidate.RelativePath == relativePath);
        Assert.True(
            file.HasFixtureFor(OracleProfile.DocComments),
            $"{file}: no committed {OracleProfile.DocComments.Suffix}. Run ./build.sh Oracle."
        );
    }

    /// <summary>
    ///     Tier A here means the fixture matches; Tier D means it does not, and both are asserted.
    /// </summary>
    [Theory]
    [MemberData(nameof(Files))]
    public void TheRecordedTier_IsWhatTheDocCommentFixtureSays(string relativePath) {
        var row = XmlDocOracle.Rows().Single(candidate => candidate.File.RelativePath == relativePath);
        Assert.True(OptionRegistry.TryResolve(row.Key, out var id));
        var tier = OptionRegistry.Get(id).Tier;

        if (tier == OptionTier.A) {
            Assert.True(
                row.Agrees,
                $"{row.Key} is Tier A and its doc-comment fixture disagrees with Skala:\n"
                + string.Join("\n", XmlDocOracle.Diff(row))
                + "\n\nTier A is a claim that Skala reproduces Rider's behaviour. Either fix the formatter or "
                + "demote the key to D with a docs/divergences.md entry carrying the shape above."
            );

            return;
        }

        // ⚠ Tier D has three honest causes here and this assertion used to admit one, then two. The
        // fixture pins the *export's* configuration; the key-flip sweep flips the key. A key can
        // reproduce its doc-comment fixture byte for byte and still diverge at another value — every
        // one of the six demoted at c0691cb7 does exactly that — and demanding `!row.Agrees` of
        // those turns a correct demotion into a test failure. What must still be caught is the
        // stale reason: Tier D with no evidence behind it from any instrument.
        if (Unswept.Contains(row.Key)) {
            // ⚠ The third cause, and the one this file had no room for. These keys were fixed and
            // now reproduce their fixtures; none of them has ever been swept, so
            // `Unsubstantiated()` cannot speak for them, and a fixture is not a Tier A claim —
            // that is the exact mistake the six demotions above were. So they sit at D with a
            // reason that is neither stale nor a divergence: *measured, agreeing, and not yet
            // swept*. The sweep on master decides, and the list shrinks to nothing when it does.
            Assert.True(
                row.Agrees,
                $"{row.Key} is on the awaiting-the-sweep list and its doc-comment fixture no longer "
                + "agrees:\n"
                + string.Join("\n", XmlDocOracle.Diff(row))
                + "\n\nThat list means 'fixed, and waiting only to be swept'. Either the fix regressed, "
                + "or the key belongs back with the divergences and needs a docs/divergences.md entry."
            );

            return;
        }

        // ⚠ The fourth cause, added 2026-08-29: the key is marked **inert** in the registry — Skala
        // reads it and it changes nothing. Agreement is then trivial rather than evidence, because a
        // key that moves no bytes cannot disagree with anything, so neither the fixture nor the sweep
        // can speak for it and Tier D is the honest mark. `skala_xmldoc_indent_size` and `indent_style` are
        // the case: measured under `OracleProfile.DocComments`, the ReSharper key moves nothing while
        // the C# indent governs the comment's inner indent. Promoting them on their agreement was
        // tried and reverted — `OptionRegistryTests.Tiers_AreHonest` and
        // `Inert_OptionsCarryAReasonAndAreNotClaimedAsImplemented` both refuse an inert Tier A, and
        // they are right to.
        if (OptionRegistry.Get(id).Inert is not null) {
            return;
        }

        Assert.True(
            !row.Agrees || SweepVerdicts.Unsubstantiated().Contains(row.Key),
            $"{row.Key} is Tier {tier}, Skala reproduces its doc-comment fixture byte for byte, and the "
            + "committed key-flip sweep does not contradict it either. Tier D in this family means "
            + "'measured against the oracle, and disagreeing' — by the fixture, or by the sweep at a value "
            + "the fixture does not reach. With neither saying so the reason is stale: promote it to A in "
            + "the same commit that fixes it and retire its docs/divergences.md entry."
        );
    }

    /// <summary>
    ///     The nine keys SK-DIV-0019 … SK-DIV-0023 covered: fixed, now agreeing, and never swept.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This list may only shrink, and nothing may be added to it without a sweep row.</b> Seven
    ///     of the nine are one fix — the wrap column was measured wrong, every one of their fixtures
    ///     wraps, and five were SK-DIV-0019 alone. The other two are SK-DIV-0022 (<c>skala_xmldoc_spaces_inside_tags
    ///     = false</c> means "do not add", not "remove the author's") and SK-DIV-0023's surviving half
    ///     (every blank <c>///</c> line the oracle writes carries the marker's space).
    ///     <para>
    ///         ⚠ They are not promoted here. Tier A is a claim about the option across its domain, and the
    ///         one instrument that could make it — the key-flip sweep — has no row for any of the nine;
    ///         six <c>resharper_xmldoc_*</c> keys were once promoted on exactly this evidence and demoted
    ///         the same afternoon, every one of them agreeing at the export's value and diverging away
    ///         from it.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>What the sweep needs before it can reach them.</b> The field <c>SweepPlan</c> reads is
    ///         <c>oracle</c> in <c>options.json</c>, and it was left null across this family when "no
    ///         fixture can pin a documentation comment" was still believed; a null glob is excluded with
    ///         "no <c>oracle</c> fixture in the registry" and the key is never swept at any value. Setting
    ///         it to <c>constructs/xmldoc/&lt;key&gt;.cs</c> is enough — <c>ScratchTree.ProfileFor</c>
    ///         already switches that subtree to <see cref="OracleProfile.DocComments" /> — and
    ///         <c>Sweep -- verify &lt;key&gt;</c> then answers.
    ///         <para>
    ///             ⚠ <b>It costs one red test until the sweep runs, and that is not a reason to leave the
    ///             glob off.</b> A Tier D key carrying a glob must have a non-Conformant row in the
    ///             committed sidecar, so
    ///             <c>OptionCoverageTests.TierD_CarriesAFixtureOnlyWhereTheSweepDemotedIt</c> is red for
    ///             each glob added before the sweep that would write that row. The first attempt at this
    ///             set all nine and reverted the lot for that reason; reverting also removed the only
    ///             thing that lets the next sweep reach them, so the gap survived the fix. Three are set
    ///             now — see below — and the red goes away in the same run that writes their rows.
    ///         </para>
    ///     </para>
    ///     <para>
    ///         ⚠ Measured with the glob in place, so the next person does not have to guess what the sweep
    ///         will say: <c>wrap_lines</c>, <c>skala_xmldoc_linebreak_before_multiline_elements</c> and
    ///         <c>skala_xmldoc_blank_line_after_pi</c> come back <b>Conformant</b> — those three are the promotions the
    ///         sweep can earn. <c>max_line_length</c> and <c>skala_xmldoc_wrap_text</c> are <b>Divergent</b> and
    ///         <c>skala_xmldoc_wrap_tags_and_pi</c> <b>Spurious</b>.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The last three of those verdicts were about the fixtures and have been withdrawn.</b>
    ///         <c>skala_xmldoc_linebreaks_inside_tags_for_elements_longer_than</c> read <b>Inert</b>, and
    ///         <c>skala_xmldoc_spaces_inside_tags</c> and <c>skala_xmldoc_linebreak_before_singleline_elements</c>
    ///         <b>Unexercised</b> — every one of them because its corpus file could not tell the key's
    ///         values apart, not because the key could not be observed:
    ///         <list type="bullet">
    ///             <item>
    ///                 <c>skala_xmldoc_spaces_inside_tags</c>'s file was written with a space inside each tag, and
    ///                 <c>false</c> means "do not add" rather than "remove" (SK-DIV-0022), so both values
    ///                 kept the author's spaces. Written tight, the key moves both engines and the sweep
    ///                 says <b>Conformant</b>.
    ///             </item>
    ///             <item>
    ///                 <c>skala_xmldoc_linebreaks_inside_tags_for_elements_longer_than</c>'s summary was long enough
    ///                 that width opened it at all three probe values — including the degenerate
    ///                 <c>0</c> and <c>1</c> an int probe offers. Shortened below the margin, the export's
    ///                 <c>2147483647</c> keeps it closed and <c>0</c>/<c>1</c> open it: <b>Conformant</b>.
    ///             </item>
    ///             <item>
    ///                 <c>skala_xmldoc_linebreak_before_singleline_elements</c>'s element sat inside a container that
    ///                 stayed flat, so the renderer never reached the break rule. With the element
    ///                 mid-line inside an opened container the sweep says <b>Divergent</b>, and the
    ///                 disagreement is one-sided and worth having: at <c>true</c> the oracle breaks both
    ///                 <em>before</em> and <em>after</em> the single-line element and Skala breaks only
    ///                 before it. Baseline agrees — at the export's <c>false</c> the two are identical —
    ///                 so the divergence is the key's and not inherited.
    ///             </item>
    ///         </list>
    ///         ⚠ The general lesson is the one <c>SweepOutcome</c>'s own remarks state and this family
    ///         proved twice: <c>INERT</c> and <c>UNEXERCISED</c> are claims about a fixture until the
    ///         fixture has been shown able to separate the values. A glob whose fixture cannot is worse
    ///         than no glob, because it reports a verdict about the corpus in the option's name.
    ///     </para>
    ///     <para>
    ///         ⚠ The assertion above runs in <em>both</em> directions over this list, so it cannot rot into
    ///         a place to park a regression: a key here that stops agreeing fails, and a key here that
    ///         earns Tier A leaves.
    ///     </para>
    /// </remarks>
    static readonly HashSet<string> Unswept = new(StringComparer.Ordinal) {
        "skala_xmldoc_max_line_length",
        "skala_xmldoc_wrap_lines",
        "skala_xmldoc_wrap_text",
        "skala_xmldoc_wrap_tags_and_pi",
        "skala_xmldoc_linebreaks_inside_tags_for_elements_longer_than",
        "skala_xmldoc_linebreak_before_multiline_elements",
        "skala_xmldoc_linebreak_before_singleline_elements",
        "skala_xmldoc_spaces_inside_tags",
        "skala_xmldoc_blank_line_after_pi"
    };

    /// <summary>
    ///     ⚠ The headline number, asserted so that it cannot drift without a diff.
    /// </summary>
    [Fact]
    public void TheSplit_IsTwentyTwoAgainstNone() {
        // ⚠ Raised from 13, and it is now the whole family. Seven of the nine were SK-DIV-0019's wrap
        // column — one arithmetic, measured wrong; the other two were SK-DIV-0022 and SK-DIV-0023's
        // surviving half. ⚠ A full house on the fixtures is *not* a full house on the tiers: nine of
        // the 22 are still Tier D because no sweep has reached them. See `Unswept`.
        var rows = XmlDocOracle.Rows();
        var agreeing = rows.Count(static row => row.Agrees);
        Assert.Equal(22, rows.Count);
        Assert.True(
            agreeing >= 22,
            $"{agreeing.ToString(CultureInfo.InvariantCulture)} of "
            + $"{rows.Count.ToString(CultureInfo.InvariantCulture)} doc-comment fixtures agree; the committed "
            + "measurement is 22. This is a ratchet and it is now at the ceiling: a fall is a regression, and "
            + "the key that fell is named by TheRecordedTier_IsWhatTheDocCommentFixtureSays."
        );
    }
}
