using System.Globalization;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

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

        // ⚠ Tier D has two honest causes here and this assertion used to admit only one. The fixture
        // pins the *export's* configuration; the key-flip sweep flips the key. A key can reproduce its
        // doc-comment fixture byte for byte and still diverge at another value — every one of the six
        // demoted at c0691cb7 does exactly that — and demanding `row.Agrees == false` of those turns a
        // correct demotion into a test failure. What must still be caught is the stale reason: Tier D
        // with no evidence behind it from either instrument.
        Assert.True(
            row.Agrees == false || SweepVerdicts.Unsubstantiated().Contains(row.Key),
            $"{row.Key} is Tier {tier}, Skala reproduces its doc-comment fixture byte for byte, and the "
            + "committed key-flip sweep does not contradict it either. Tier D in this family means "
            + "'measured against the oracle, and disagreeing' — by the fixture, or by the sweep at a value "
            + "the fixture does not reach. With neither saying so the reason is stale: promote it to A in "
            + "the same commit that fixes it and retire its docs/divergences.md entry."
        );
    }

    /// <summary>
    ///     ⚠ The headline number, asserted so that it cannot drift without a diff.
    /// </summary>
    [Fact]
    public void TheSplit_IsThirteenAgainstNine() {
        var rows = XmlDocOracle.Rows();
        var agreeing = rows.Count(static row => row.Agrees);
        Assert.Equal(22, rows.Count);
        Assert.True(
            agreeing >= 13,
            $"{agreeing.ToString(CultureInfo.InvariantCulture)} of "
            + $"{rows.Count.ToString(CultureInfo.InvariantCulture)} doc-comment fixtures agree; the committed "
            + "measurement is 13. This is a ratchet: agreement may rise, and a rise is a commit that promotes "
            + "the keys it earned."
        );
    }
}
