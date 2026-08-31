using Rikarin.Skala.Testing;
using System.Globalization;
using System.Text.Json;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>The ratchet: fidelity may not decrease, and improving it is a commit.</summary>
/// <param name="Basis">
///     ⚠ Which lines the two numbers are over, spelled out in the file rather than remembered. It is
///     <c>outside doc comments</c> for every set, and <see cref="Read" /> refuses any other value rather
///     than comparing a number against a baseline drawn over a different population — which is the
///     failure docs/plan/12 § "A ratchet compares numbers over the same population" describes.
/// </param>
public sealed record FidelityBaseline(double LineFidelity, double FileFidelity, string Milestone, string Basis) {
    public static string Path { get; } = System.IO.Path.Combine(Corpus.Root, "fidelity.json");

    public static IReadOnlyDictionary<string, FidelityBaseline> Read() {
        var baselines = JsonSerializer.Deserialize<Dictionary<string, FidelityBaseline>>(File.ReadAllText(Path))
            ?? throw new InvalidOperationException($"{Path} is empty.");

        foreach (var (set, baseline) in baselines) {
            if (!string.Equals(
                    baseline.Basis,
                    FidelityReport.Name(FidelityBasis.OutsideDocComments),
                    StringComparison.Ordinal
                )) {
                throw new InvalidOperationException(
                    $"{Path}: '{set}' records a baseline over '{baseline.Basis}' and the differential measures "
                    + $"'{FidelityReport.Name(FidelityBasis.OutsideDocComments)}'. A ratchet compares numbers over "
                    + "the same population; re-measure and re-base rather than comparing these two."
                );
            }
        }

        return baselines;
    }
}

/// <summary>
///     Level 2 of docs/plan/12: the number that matters.
/// </summary>
/// <remarks>
///     ⚠ The output of a differential run is not pass/fail, it is a ranked report of divergence classes
///     by line count — the work queue. What is pass/fail is the ratchet: a commit may raise the number
///     and may not lower it. The report is written to <c>.skala/conformance.md</c> on every run so that
///     a regression comes with its own diagnosis.
/// </remarks>
public sealed class DifferentialTests {
    static FidelityReport Measure(string set, FidelityBasis basis = FidelityBasis.OutsideDocComments) {
        var files = Corpus.Files(set).Where(static file => file.HasFixture).ToArray();
        var results = new List<(string File, string Expected, string Actual)>(files.Length);
        foreach (var file in files) {
            results.Add((file.ToString(), OracleFixture.Read(file), CorpusFormatter.Format(file).Formatted));
        }

        return Fidelity.Compare(results, basis);
    }

    /// <summary>
    ///     ⚠ The ratchet is over <see cref="FidelityBasis.OutsideDocComments" />, and that is stated in
    ///     every message it prints.
    /// </summary>
    /// <remarks>
    ///     ⚠ It was over every line until the documentation-comment sub-formatter became the default.
    ///     It cannot stay there: Skala runs ReSharper's "Reformat embedded XML doc comments" and the
    ///     pinned oracle profile does not, so a <c>///</c> line's disagreement is a fact about the
    ///     profile rather than about the formatter, and a ratchet built on it would ratchet the wrong
    ///     thing. Both numbers are recorded at the re-base in <c>fidelity.json</c>'s
    ///     <c>Milestone</c> field so that the population change is visible rather than inferred.
    /// </remarks>
    [Theory]
    [InlineData(Corpus.Real)]
    [InlineData(Corpus.Constructs)]
    [InlineData(Corpus.Pathological)]
    public void Fidelity_DoesNotDecrease(string set) {
        var baseline = FidelityBaseline.Read()[set];
        var report = Measure(set);
        Write(set, report, baseline);

        Assert.True(
            report.LineFidelity >= baseline.LineFidelity - 0.0001,
            $"Line fidelity ({report.BasisName}) on {set} fell from {baseline.LineFidelity * 100:F2}% to {report.LineFidelity * 100:F2}%.\n"
            + "⚠ The gates are cumulative and the next milestone measures against this baseline, so a merged\n"
            + "regression corrupts everything after it. The ranked divergence classes are the work queue:\n\n"
            + report.Render(8)
        );

        Assert.True(
            report.FileFidelity >= baseline.FileFidelity - 0.0001,
            $"File fidelity ({report.BasisName}) on {set} fell from {baseline.FileFidelity * 100:F2}% to {report.FileFidelity * 100:F2}%."
        );
    }

    [Fact]
    public void LineFidelity_MeetsTheMilestoneBar() {
        // docs/plan/15 § M2: "line fidelity ≥ 93 % on corpus/real/".
        var report = Measure(Corpus.Real);
        Assert.True(
            report.LineFidelity >= 0.93,
            $"Milestone 2's bar is 93 % line fidelity ({report.BasisName}) on corpus/real/; the measurement is {report.LineFidelity * 100:F2}%.\n\n"
            + report.Render(10)
        );
    }

    /// <summary>
    ///     ⚠ The number the exclusion hides, asserted rather than left to a report nobody runs.
    /// </summary>
    /// <remarks>
    ///     ⚠ An excluded category that is never looked at again is an excluded category that can grow
    ///     without anyone noticing. This is the every-line number over the same corpus: it is expected
    ///     to be *lower* than the ratchet's, by exactly the amount ReSharper's XML doc cleanup task
    ///     would move if the pinned profile ran it, and its floor is here so that "the doc comments
    ///     diverge, as designed" can never quietly become "everything diverges".
    /// </remarks>
    [Fact]
    public void TheEveryLineNumber_IsStillReported() {
        var outside = Measure(Corpus.Real);
        var everyLine = Measure(Corpus.Real, FidelityBasis.EveryLine);

        Assert.True(
            everyLine.LineFidelity <= outside.LineFidelity + 0.0001,
            $"Every-line fidelity ({everyLine.LineFidelity * 100:F2}%) exceeds the outside-doc-comments number "
            + $"({outside.LineFidelity * 100:F2}%). The exclusion can only remove disagreement, never add it, so "
            + "one of the two is measuring something other than what it says."
        );

        Assert.True(
            everyLine.LineFidelity >= 0.93,
            $"Every-line fidelity on corpus/real/ is {everyLine.LineFidelity * 100:F2}%, below milestone 2's 93 % bar. "
            + "The doc-comment exclusion is not a licence for the rest to drift.\n\n"
            + everyLine.Render(10)
        );
    }

    [Fact]
    public void EveryCorpusFile_HasAnOracleFixture() {
        // A corpus file with no fixture is a file that is not measured, which is worse than not
        // having it: it looks like coverage.
        var missing = Corpus.All()
            .Where(static file => !file.HasFixture)
            .Select(static file => file.ToString())
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            missing.Length == 0,
            $"{missing.Length.ToString(CultureInfo.InvariantCulture)} corpus file(s) have no committed .expected.cs. Run ./build.sh Oracle: "
            + string.Join(", ", missing.Take(10))
        );
    }

    [Fact]
    public void EveryFixture_RecordsTheReSharperVersionThatProducedIt() {
        foreach (var file in Corpus.All().Where(static file => file.HasFixture)) {
            var header = OracleFixture.ReadHeader(file);
            Assert.True(header is not null, $"{file}: the fixture has no `// skala-oracle:` header.");
            Assert.False(
                string.IsNullOrEmpty(header!.ReSharperVersion),
                $"{file}: the fixture records no ReSharper version."
            );
            Assert.NotEqual("unknown", header.ReSharperVersion);
        }
    }

    [Fact]
    public void TheDivergenceRegister_IsReadable() {
        // Every SK-DIV entry that exists must parse; the count is published with the fidelity number.
        Assert.NotEmpty(Divergences.Register);
        Assert.All(Divergences.Register, entry => Assert.StartsWith("SK-DIV-", entry.Id, StringComparison.Ordinal));
        Assert.All(Divergences.Register, entry => Assert.NotEmpty(entry.Summary));
    }

    static void Write(string set, FidelityReport report, FidelityBaseline baseline) {
        try {
            var directory = Path.Combine(Corpus.RepositoryRoot, ".skala");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, $"conformance-{set}.md"),
                $"# Conformance — {set}\n\nbaseline: {baseline.LineFidelity * 100:F2}% ({baseline.Milestone})\n\n```\n{report.Render(25)}```\n"
            );
        } catch (IOException) {
            // The report is a convenience; a read-only working tree does not fail the suite.
        }
    }
}
