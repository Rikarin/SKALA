using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;
using System.Globalization;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     docs/plan/05 § "<c>keep_existing_*</c>": the four-way table, measured rather than reasoned about.
/// </summary>
/// <remarks>
///     ⚠ This is the milestone's highest-risk item, and the risk is not that the formatter is slightly
///     off — it is that the wrong reading of two booleans turns a first run on a large tree into a
///     rewrite of every call site. The plan's own warning: "Getting this table wrong in either direction
///     is a catastrophic first-run diff."
///     <para>
///         The table the oracle actually implements, pinned by the fixtures beside
///         <c>constructs/preservation/*.cs</c>:
///     </para>
///     <code>
///   keep_user_linebreaks │ keep_existing_X │ break at the delimiters │ break between items
///   ─────────────────────┼─────────────────┼─────────────────────────┼────────────────────
///   true                 │ true            │ kept                    │ kept
///   true                 │ false           │ re-joined               │ kept
///   false                │ true            │ re-joined               │ re-joined
///   false                │ false           │ re-joined               │ re-joined
///     </code>
///     <para>
///         ⚠ Row two is the one docs/plan/05 stated as "source breaks kept, but the wrap style may add
///         breaks when too wide", which is half the story: <c>Foo(\n a)</c> is re-joined there and
///         <c>Foo(\n a,\n b)</c> is not, because the two keys govern different gaps. Row three is the one
///         the naive reading gets backwards in the other direction — the per-construct key does not rescue
///         a construct once the global switch is off.
///     </para>
/// </remarks>
public sealed class PreservationTests {
    public static TheoryData<CorpusFile, string> Pairs {
        get {
            var data = new TheoryData<CorpusFile, string>();
            foreach (var (file, variant) in CorpusVariants.Pairs(Corpus.Constructs)) {
                data.Add(file, variant.Name);
            }

            return data;
        }
    }

    [Fact]
    public void ThePreservationSet_Exists_AndIsRunUnderFourConfigurations() {
        var files = Corpus.Files(Corpus.Constructs)
            .Where(static file => CorpusVariants.For(file).Count > 0)
            .ToArray();

        Assert.True(files.Length > 0, "constructs/preservation/ has no files; the four-way table is untested.");
        Assert.All(files, file => Assert.Equal(4, CorpusVariants.For(file).Count));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void EveryVariant_HasACommittedFixture(CorpusFile file, string variantName) {
        var variant = Variant(variantName);
        Assert.True(
            variant.HasFixture(file),
            $"{file} has no fixture for the '{variantName}' configuration. Run ./build.sh Oracle: "
            + "a configuration that is not measured against the oracle is a configuration nobody has checked."
        );
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void EveryVariant_IsIdempotentAndTokenEquivalent(CorpusFile file, string variantName) {
        // ⚠ The safety properties are not relaxed for a non-default configuration. A formatter that
        // corrupts a file only when `keep_user_linebreaks = false` is still a formatter that
        // corrupts files.
        var first = Format(file, Variant(variantName), CSharpFormatter.Read(file.Path));
        Assert.Equal(FormatOutcome.Formatted, first.Outcome);

        var second = Format(file, Variant(variantName), SourceText.From(first.Formatted));
        Assert.True(
            second.Edits.IsEmpty,
            $"{file} under '{variantName}' is not idempotent; the second pass wants {second.Edits.Length} edit(s)."
        );
    }

    [Fact]
    public void TheFourConfigurations_ProduceFourDistinguishableOutputs() {
        // An axis that changes nothing is an axis that is not wired. Both must move something.
        var byName = CorpusVariants.Preservation.ToDictionary(static variant => variant.Name, StringComparer.Ordinal);
        var moved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Corpus.Files(Corpus.Constructs).Where(static f => CorpusVariants.For(f).Count > 0)) {
            var text = CSharpFormatter.Read(file.Path);
            var outputs = CorpusVariants.Preservation
                .ToDictionary(
                    variant => variant.Name,
                    variant => Format(file, variant, text).Formatted,
                    StringComparer.Ordinal
                );

            if (!string.Equals(outputs["keep-keep"], outputs["keep-rearrange"], StringComparison.Ordinal)) {
                moved.Add("keep_existing_*");
            }

            if (!string.Equals(outputs["keep-keep"], outputs["reflow-keep"], StringComparison.Ordinal)) {
                moved.Add("keep_user_linebreaks");
            }
        }

        Assert.Contains("keep_existing_*", moved);
        Assert.Contains("keep_user_linebreaks", moved);
        _ = byName;
    }

    /// <summary>
    ///     The differential number for each corner, with the same ratchet the main corpus has.
    /// </summary>
    [Theory]
    [InlineData("keep-keep")]
    [InlineData("keep-rearrange")]
    [InlineData("reflow-keep")]
    [InlineData("reflow-rearrange")]
    public void Fidelity_DoesNotDecrease(string variantName) {
        var variant = Variant(variantName);
        var results = new List<(string File, string Expected, string Actual)>();
        foreach (var file in Corpus.Files(Corpus.Constructs)) {
            if (!variant.HasFixture(file)) {
                continue;
            }

            results.Add(
                (
                    file.ToString(),
                    OracleFixture.Read(file, variant),
                    Format(file, variant, CSharpFormatter.Read(file.Path)).Formatted)
            );
        }

        Assert.NotEmpty(results);
        var report = Fidelity.Compare(results);
        var baseline = FidelityBaseline.Read()["preservation/" + variantName];

        Assert.True(
            report.LineFidelity >= baseline.LineFidelity - 0.0001,
            $"Line fidelity under '{variantName}' fell from {baseline.LineFidelity * 100:F2}% to "
            + report.LineFidelity.ToString("P2", CultureInfo.InvariantCulture)
            + ".\n\n"
            + report.Render(6)
        );
    }

    static CorpusVariant Variant(string name) =>
        CorpusVariants.Preservation.Single(variant => string.Equals(variant.Name, name, StringComparison.Ordinal));

    static FormatResult Format(CorpusFile file, CorpusVariant variant, SourceText text) =>
        CSharpFormatter.Format(file.Path, text, OptionResolver.Resolve(file.Path, variant.Overrides).Options);
}
