using System.Globalization;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
/// The differential over <em>degraded</em> input — docs/plan/12 § "The unformat differential".
/// </summary>
/// <remarks>
/// ⚠ These are the ratchets for the second differential, and the reason there is a second one is a
/// number: <c>corpus/real/</c>'s inputs are already 92.08 % line-identical to their fixtures, so the
/// 99.63 % headline is mostly a measurement of Skala leaving good code alone. Here the input's
/// formatting has been destroyed first, so what is measured is whether Skala <em>decides</em> what
/// ReSharper decides.
/// <para>
/// ⚠ Every number is asserted against the null hypothesis as well as against its baseline. A ratchet
/// on its own cannot tell "the formatter improved" from "the corpus got easier", and
/// <see cref="TheNullHypothesis_IsFarBelowSkala"/> is what fails if a future regeneration softens
/// the degradation.
/// </para>
/// </remarks>
public sealed class UnformatTests {
    public static TheoryData<UnformatMode> Modes {
        get {
            var data = new TheoryData<UnformatMode>();
            foreach (var mode in Unformat.Modes) {
                data.Add(mode);
            }

            return data;
        }
    }

    /// <summary>
    /// ⚠ Re-checked from the committed bytes rather than trusted because the generator said so.
    /// </summary>
    /// <remarks>
    /// A degraded input is one half of a fixture pair. A hand-edit to it — tidying a line, fixing
    /// what looks like a typo — turns every subsequent measurement into a comparison of two
    /// unrelated files, and nothing else in the suite would notice.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Modes))]
    public void EveryDegradedFile_IsTheSameProgramAsItsSource(UnformatMode mode) {
        var sources = Corpus.Files(Corpus.Real).ToDictionary(static file => file.RelativePath, StringComparer.Ordinal);
        var files = UnformatCorpus.Files(mode);
        Assert.NotEmpty(files);

        foreach (var file in files) {
            var relative = file.RelativePath[(Unformat.Name(mode).Length + 1)..];
            Assert.True(sources.TryGetValue(relative, out var source), $"{file}: no corpus/real/ source.");
            Assert.True(
                Unformat.IsSameProgram(File.ReadAllText(source!.Path), File.ReadAllText(file.Path)),
                $"{file} is no longer the same program as corpus/real/{relative}. "
                + "The degraded corpus is generated, not hand-edited: "
                + "`dotnet run --project Testing/Rikarin.Skala.Testing -- unformat regenerate`."
            );
        }
    }

    /// <summary>A degradation that degraded nothing is a fixture pair that measures nothing.</summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public void EveryDegradedFile_ActuallyDiffersFromItsSource(UnformatMode mode) {
        var sources = Corpus.Files(Corpus.Real).ToDictionary(static file => file.RelativePath, StringComparer.Ordinal);
        foreach (var file in UnformatCorpus.Files(mode)) {
            var relative = file.RelativePath[(Unformat.Name(mode).Length + 1)..];
            Assert.NotEqual(
                TextNormalisation.Normalise(File.ReadAllText(sources[relative].Path)),
                TextNormalisation.Normalise(File.ReadAllText(file.Path))
            );
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EveryDegradedFile_HasAnOracleFixture(UnformatMode mode) {
        var missing = UnformatCorpus.Files(mode)
            .Where(static file => !file.HasFixture)
            .Select(static file => file.ToString())
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"{missing.Length.ToString(CultureInfo.InvariantCulture)} degraded file(s) have no committed "
            + ".expected.cs. `unformat oracle`: " + string.Join(", ", missing.Take(8))
        );

        foreach (var file in UnformatCorpus.Files(mode)) {
            var header = OracleFixture.ReadHeader(file);
            Assert.True(header is not null, $"{file}: the fixture has no `// skala-oracle:` header.");
            Assert.NotEqual("unknown", header!.ReSharperVersion);
        }
    }

    /// <summary>The ratchet, over its own population.</summary>
    /// <remarks>
    /// ⚠ Its own entries in <c>fidelity.json</c>, beside the existing ones rather than replacing
    /// them. The two differentials answer different questions over different inputs and neither
    /// number is the other's successor.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Modes))]
    public void UnformatFidelity_DoesNotDecrease(UnformatMode mode) {
        var key = UnformatCorpus.Set + "/" + Unformat.Name(mode);
        var baseline = FidelityBaseline.Read()[key];
        var result = UnformatDifferential.Measure(mode, CorpusFormatter.Symbols);
        Assert.NotNull(result);

        Assert.True(
            result!.Bare.LineFidelity >= baseline.LineFidelity - 0.0001,
            $"Line fidelity on {key} fell from {baseline.LineFidelity * 100:F2}% to {result.Bare.LineFidelity * 100:F2}%.\n"
            + $"⚠ The null hypothesis on this population is {result.Null.LineFidelity * 100:F2}%; "
            + "the gap between the two is the only part that is the formatter's.\n\n"
            + result.Bare.Render(8)
        );

        Assert.True(
            result.Bare.FileFidelity >= baseline.FileFidelity - 0.0001,
            $"File fidelity on {key} fell from {baseline.FileFidelity * 100:F2}% to {result.Bare.FileFidelity * 100:F2}%."
        );
    }

    /// <summary>
    /// ⚠ The calibration, asserted rather than printed.
    /// </summary>
    /// <remarks>
    /// A ratchet on its own cannot tell a formatter that improved from a corpus that got easier. If
    /// a future regeneration softens the degradation — a weight nudged, a mode quietly narrowed —
    /// the null hypothesis rises towards the measured number and this fails, which is the only place
    /// that failure is visible. The margins are wide on purpose: they are a tripwire on the corpus,
    /// not a second bar on the formatter.
    /// </remarks>
    [Fact]
    public void TheNullHypothesis_IsFarBelowSkala() {
        foreach (var mode in Unformat.Modes) {
            var result = UnformatDifferential.Measure(mode, CorpusFormatter.Symbols);
            Assert.NotNull(result);
            Assert.True(
                result!.Null.LineFidelity < 0.80,
                $"{Unformat.Name(mode)}: returning the degraded input unchanged now scores "
                + $"{result.Null.LineFidelity * 100:F2}% of lines against the oracle. The degradation has "
                + "stopped degrading, and the differential over it has stopped discriminating."
            );

            Assert.True(
                result.Bare.LineFidelity > result.Null.LineFidelity + 0.10,
                $"{Unformat.Name(mode)}: Skala scores {result.Bare.LineFidelity * 100:F2}% where doing nothing "
                + $"scores {result.Null.LineFidelity * 100:F2}%."
            );
        }
    }
}
