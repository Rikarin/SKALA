using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;
using System.Globalization;
using System.Text.Json;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>The ratchet: fidelity may not decrease, and improving it is a commit.</summary>
/// <param name="Basis">
///     ⚠ Which lines the two numbers are over, spelled out in the file rather than remembered, and every
///     consumer measures over the basis its own entry records — never another. <c>constructs</c>,
///     <c>real</c> and <c>pathological</c> are <c>every line</c> since #449 regenerated them with the
///     doc-comment task on, and <see cref="DifferentialTests" /> asserts it; the preservation variants and
///     the unformat differential stay <c>outside doc comments</c> until their own fixtures are regenerated.
///     <see cref="Read" /> refuses a name it does not know rather than comparing a number against a
///     baseline drawn over a different population — docs/plan/12 § "A ratchet compares numbers over the
///     same population".
/// </param>
public sealed record FidelityBaseline(double LineFidelity, double FileFidelity, string Milestone, string Basis) {
    public static string Path { get; } = System.IO.Path.Combine(Corpus.Root, "fidelity.json");

    /// <summary>The recorded basis, as the differential's own enum.</summary>
    public FidelityBasis Kind =>
        string.Equals(Basis, FidelityReport.Name(FidelityBasis.EveryLine), StringComparison.Ordinal)
            ? FidelityBasis.EveryLine
            : FidelityBasis.OutsideDocComments;

    public static IReadOnlyDictionary<string, FidelityBaseline> Read() {
        var baselines = JsonSerializer.Deserialize<Dictionary<string, FidelityBaseline>>(File.ReadAllText(Path))
            ?? throw new InvalidOperationException($"{Path} is empty.");

        foreach (var (set, baseline) in baselines) {
            if (!string.Equals(baseline.Basis, FidelityReport.Name(baseline.Kind), StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    $"{Path}: '{set}' records a baseline over '{baseline.Basis}', which is not a basis the "
                    + "differential measures. A ratchet compares numbers over the same population; re-measure and "
                    + "re-base rather than comparing across two."
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
    static FidelityReport Measure(string set, FidelityBasis basis = FidelityBasis.EveryLine) {
        var files = Corpus.Files(set).Where(static file => file.HasFixture).ToArray();
        var results = new List<(string File, string Expected, string Actual)>(files.Length);
        foreach (var file in files) {
            // ⚠ Under the oracle's own symbols (#588): the fixtures are its output with them, and without them
            // Skala formats `#if` bodies the oracle never saw as code.
            var formatted = CSharpFormatter.Format(
                    file.Path,
                    CSharpFormatter.Read(file.Path),
                    CorpusFormatter.OptionsFor(file.Path),
                    null,
                    Corpus.OracleSymbols
                )
                .Formatted;
            results.Add((file.ToString(), OracleFixture.Read(file), formatted));
        }

        return Fidelity.Compare(results, basis);
    }

    /// <summary>
    ///     ⚠ The ratchet is over <see cref="FidelityBasis.EveryLine" />, and that is stated in every
    ///     message it prints.
    /// </summary>
    /// <remarks>
    ///     ⚠ It was over every line until the documentation-comment sub-formatter became the default, then
    ///     over the lines outside doc comments while the pinned format-only profile did not run ReSharper's
    ///     "Reformat embedded XML doc comments" and Skala did — a <c>///</c> line's disagreement was then a
    ///     fact about the profile rather than about the formatter. #449 put the task in the profile and
    ///     regenerated the fixtures, so it is every line again. Both numbers are recorded at each re-base in
    ///     <c>fidelity.json</c>'s <c>Milestone</c> field so that the population change is visible rather
    ///     than inferred.
    /// </remarks>
    [Theory]
    [InlineData(Corpus.Real)]
    [InlineData(Corpus.Constructs)]
    [InlineData(Corpus.Pathological)]
    public void Fidelity_DoesNotDecrease(string set) {
        var baseline = FidelityBaseline.Read()[set];
        Assert.True(
            baseline.Kind == FidelityBasis.EveryLine,
            $"{set} records its baseline over '{baseline.Basis}'; since #449 its fixtures format doc comments and "
            + "the ratchet is over every line."
        );

        var report = Measure(set, baseline.Kind);
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
    ///     ⚠ The two bases side by side, asserted rather than left to a report nobody runs.
    /// </summary>
    /// <remarks>
    ///     ⚠ Written when the ratchet excluded <c>///</c> lines, to keep the excluded category watched.
    ///     Since #449 the ratchet is the every-line number and the doc-comment lines are measured against an
    ///     oracle that formats them; what this still asserts is that excluding them can only remove
    ///     disagreeing lines, and that the every-line number keeps milestone 2's floor. ⚠ Counted in lines,
    ///     not as a ratio: once the oracle formats doc comments too, they can agree at a higher rate than
    ///     the code around them, and removing them can then lower the percentage while removing no
    ///     disagreement at all.
    /// </remarks>
    [Fact]
    public void TheEveryLineNumber_IsStillReported() {
        var outside = Measure(Corpus.Real, FidelityBasis.OutsideDocComments);
        var everyLine = Measure(Corpus.Real);

        Assert.True(
            everyLine.Lines - everyLine.IdenticalLines >= outside.Lines - outside.IdenticalLines,
            $"Every line disagrees on {everyLine.Lines - everyLine.IdenticalLines} lines and the lines outside doc "
            + $"comments on {outside.Lines - outside.IdenticalLines}. The exclusion can only remove disagreement, "
            + "never add it, so one of the two is measuring something other than what it says."
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
        Assert.All(
            Divergences.Register,
            static entry => Assert.StartsWith("SK-DIV-", entry.Id, StringComparison.Ordinal)
        );
        Assert.All(Divergences.Register, static entry => Assert.NotEmpty(entry.Summary));
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
