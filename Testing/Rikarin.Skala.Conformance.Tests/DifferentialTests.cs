using System.Globalization;
using System.Text.Json;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>The ratchet: fidelity may not decrease, and improving it is a commit.</summary>
public sealed record FidelityBaseline(double LineFidelity, double FileFidelity, string Milestone) {
    public static string Path { get; } = System.IO.Path.Combine(Corpus.Root, "fidelity.json");

    public static IReadOnlyDictionary<string, FidelityBaseline> Read() =>
        JsonSerializer.Deserialize<Dictionary<string, FidelityBaseline>>(File.ReadAllText(Path))
        ?? throw new InvalidOperationException($"{Path} is empty.");
}

/// <summary>
/// Level 2 of docs/plan/12: the number that matters.
/// </summary>
/// <remarks>
/// ⚠ The output of a differential run is not pass/fail, it is a ranked report of divergence classes
/// by line count — the work queue. What is pass/fail is the ratchet: a commit may raise the number
/// and may not lower it. The report is written to <c>.skala/conformance.md</c> on every run so that
/// a regression comes with its own diagnosis.
/// </remarks>
public sealed class DifferentialTests {
    static FidelityReport Measure(string set) {
        var files = Corpus.Files(set).Where(static file => file.HasFixture).ToArray();
        var results = new List<(string File, string Expected, string Actual)>(files.Length);
        foreach (var file in files) {
            results.Add((file.ToString(), OracleFixture.Read(file), CorpusFormatter.Format(file).Formatted));
        }

        return Fidelity.Compare(results);
    }

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
            $"Line fidelity on {set} fell from {baseline.LineFidelity * 100:F2}% to {report.LineFidelity * 100:F2}%.\n"
            + "⚠ The gates are cumulative and the next milestone measures against this baseline, so a merged\n"
            + "regression corrupts everything after it. The ranked divergence classes are the work queue:\n\n"
            + report.Render(8));

        Assert.True(
            report.FileFidelity >= baseline.FileFidelity - 0.0001,
            $"File fidelity on {set} fell from {baseline.FileFidelity * 100:F2}% to {report.FileFidelity * 100:F2}%.");
    }

    [Fact]
    public void LineFidelity_MeetsTheMilestoneBar() {
        // docs/plan/15 § M2: "line fidelity ≥ 93 % on corpus/real/".
        var report = Measure(Corpus.Real);
        Assert.True(
            report.LineFidelity >= 0.93,
            $"Milestone 2's bar is 93 % line fidelity on corpus/real/; the measurement is {report.LineFidelity * 100:F2}%.\n\n"
            + report.Render(10));
    }

    [Fact]
    public void EveryCorpusFile_HasAnOracleFixture() {
        // A corpus file with no fixture is a file that is not measured, which is worse than not
        // having it: it looks like coverage.
        var missing = Corpus.All().Where(static file => !file.HasFixture).Select(static file => file.ToString()).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            missing.Length == 0,
            $"{missing.Length.ToString(CultureInfo.InvariantCulture)} corpus file(s) have no committed .expected.cs. Run ./build.sh Oracle: "
            + string.Join(", ", missing.Take(10)));
    }

    [Fact]
    public void EveryFixture_RecordsTheReSharperVersionThatProducedIt() {
        foreach (var file in Corpus.All().Where(static file => file.HasFixture)) {
            var header = OracleFixture.ReadHeader(file);
            Assert.True(header is not null, $"{file}: the fixture has no `// skala-oracle:` header.");
            Assert.False(string.IsNullOrEmpty(header!.ReSharperVersion), $"{file}: the fixture records no ReSharper version.");
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
                $"# Conformance — {set}\n\nbaseline: {baseline.LineFidelity * 100:F2}% ({baseline.Milestone})\n\n```\n{report.Render(25)}```\n");
        } catch (IOException) {
            // The report is a convenience; a read-only working tree does not fail the suite.
        }
    }
}
