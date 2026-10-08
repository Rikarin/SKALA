using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
///     ⚠ #398: what <c>plain</c> and <c>agent</c> carry of the run's own non-blocking diagnostics.
/// </summary>
/// <remarks>
///     Before #398 the answer was nothing. <c>plain</c> printed <see cref="Renderer.Blocking" /> and
///     the findings; <c>agent</c> the banner and the findings; and on Skala's own self-gate the three
///     <c>SK9021</c> lines the SARIF carries — two files in no compilation and the coverage summary —
///     printed zero lines under either. The decision these pin (docs/plan/09 § "What the bounded
///     surfaces carry of the run's own diagnostics"): every tool diagnostic at warning or above is
///     printed on both, in each format's own shape and on stdout; info is printed on neither; the
///     banner and the exit code are untouched.
///     <para>
///         The shapes are exactly what <c>BinlogLoader.ReportStaleness</c> emits. Sabotage: drop
///         <c>Notices</c> from <c>Renderer.PlainDiagnostics</c> or the <c>Warnings</c> call from
///         <c>AgentRenderer.Render</c> and the positive cases here go red.
///     </para>
/// </remarks>
public sealed class ToolWarningSurfaceTests {
    const string NotAnalysed = "SK9021";

    const string NoCompilation =
        "the binary log names no compilation containing this file, so it was not analysed; rebuild";

    static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "skala-398"));
    static readonly string Binlog = Path.Combine(Root, "artifacts", "skala.binlog");
    static readonly string Orphan = Path.Combine(Root, "build", "Build.cs");

    static RunReport Report(params SkalaDiagnostic[] diagnostics) =>
        new() {
            RepositoryRoot = Root,
            Mode = LoadMode.Binlog,
            LoadSummary = "binlog",
            FileCount = 3,
            Diagnostics = [..diagnostics]
        };

    static SkalaDiagnostic Coverage() =>
        new(
            NotAnalysed,
            SkalaSeverity.Warning,
            "the binary log covers 2 of 3 selected source file(s) (67 %); "
            + "1 were in no compilation and were not analysed",
            Binlog,
            1,
            "⚠ An incremental build's binlog holds only the projects MSBuild rebuilt. Rebuild with `--no-incremental`."
        );

    static SkalaDiagnostic Unanalysed() => new(NotAnalysed, SkalaSeverity.Warning, NoCompilation, Orphan);

    /// <summary>Exactly what <c>DocumentationComments</c> emits: info, at the root.</summary>
    static SkalaDiagnostic DocumentationOff() =>
        new(
            ConfigDiagnosticIds.DocumentationDiagnosticsOff,
            SkalaSeverity.Info,
            "1 of 1 project does not set GenerateDocumentationFile",
            Root
        );

    /// <summary>Exactly what <c>ProjectLoader</c> emits on a fallback: info, at the root.</summary>
    static SkalaDiagnostic FellBack() =>
        new(
            ConfigDiagnosticIds.LoadModeFellBack,
            SkalaSeverity.Info,
            "--load=binlog could not run; falling back",
            Root
        );

    static Finding Finding() =>
        new() {
            RuleId = "SK1010",
            Severity = SkalaSeverity.Info,
            Message = "Use `is not null` instead of `!= null`",
            Path = Path.Combine(Root, "src", "Lib.cs"),
            Line = 4,
            Column = 9,
            EndLine = 4,
            EndColumn = 24
        };

    [Fact]
    public void Plain_PrintsAFileThatWasNotAnalysed_InTheShapeEveryErrorParserReads() {
        var text = Renderer.Render(
            Report(Coverage(), Unanalysed()) with { Findings = [Finding()] },
            ReportFormat.Plain
        );

        Assert.Equal(
            "artifacts/skala.binlog:1:1: warning SK9021: the binary log covers 2 of 3 selected source file(s) (67 %); "
            + "1 were in no compilation and were not analysed — ⚠ An incremental build's binlog holds only the "
            + "projects MSBuild rebuilt. Rebuild with `--no-incremental`.\n"
            + "build/Build.cs:1:1: warning SK9021: "
            + NoCompilation
            + "\n"
            + "src/Lib.cs:4:9: suggestion SK1010: Use `is not null` instead of `!= null`\n",
            text
        );
    }

    /// <summary>
    ///     ⚠ Every line of <c>plain</c> stays one <c>path:line:col: level id: message</c>, so the editor
    ///     parser this format exists for reads the tool warnings as problems at the file they name.
    /// </summary>
    [Fact]
    public void Plain_EveryLineStillMatchesTheErrorParserShape() {
        var text = Renderer.Render(
            Report(Coverage(), Unanalysed(), DocumentationOff(), FellBack()) with { Findings = [Finding()] },
            ReportFormat.Plain
        );

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.All(
            lines,
            static line => Assert.Matches(@"^[^:\s][^:]*:\d+:\d+: (error|warning|suggestion|hint) SK\d{4}: \S", line)
        );
    }

    [Fact]
    public void Agent_PrintsAFileThatWasNotAnalysed_AboveTheBuckets_AndNotAsTheBanner() {
        var text = Renderer.Render(
            Report(Coverage(), Unanalysed()) with { Findings = [Finding()] },
            ReportFormat.Agent
        );

        Assert.StartsWith(
            "WARNING 2 warnings about this run, not about your code — "
            + "the findings below may not cover what these name:\n"
            + "  SK9021  artifacts/skala.binlog  the binary log covers 2 of 3 selected source file(s) (67 %); "
            + "1 were in no compilation and were not analysed\n"
            + "        → ⚠ An incremental build's binlog holds only the projects MSBuild rebuilt. Rebuild with "
            + "`--no-incremental`.\n"
            + "  SK9021  build/Build.cs  "
            + NoCompilation
            + "\n\n"
            + "ACTION  1 finding needs a decision\n",
            text
        );
        Assert.DoesNotContain("INCOMPLETE", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The sentence reserved for a clean run is not printed over one that says a file was never
    ///     looked at: the reader this format exists for acts on <c>OK</c>.
    /// </summary>
    [Fact]
    public void Agent_DoesNotSayNothingToDo_OverAFileThatWasNotAnalysed() {
        var text = Renderer.Render(Report(Unanalysed()), ReportFormat.Agent);

        Assert.StartsWith("WARNING 1 warning about this run", text, StringComparison.Ordinal);
        Assert.Contains("  SK9021  build/Build.cs  " + NoCompilation, text, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing to do", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The decision for info: neither bounded surface prints it. <c>SK9032</c> is true of 31 of
    ///     31 projects in this repository on every run; <c>SK9025</c>'s consequence is the
    ///     <c>SKIPPED</c> line. The clean case is byte-identical to a report with no diagnostics.
    /// </summary>
    [Fact]
    public void InfoToolDiagnostics_ReachNeitherBoundedSurface() {
        var noisy = Report(DocumentationOff(), FellBack()) with { Findings = [Finding()] };
        var clean = Report() with { Findings = [Finding()] };

        Assert.Equal(Renderer.Render(clean, ReportFormat.Plain), Renderer.Render(noisy, ReportFormat.Plain));
        Assert.Equal(Renderer.Render(clean, ReportFormat.Agent), Renderer.Render(noisy, ReportFormat.Agent));
        Assert.Equal(
            "OK  nothing to do.\n",
            Renderer.Render(Report(DocumentationOff(), FellBack()), ReportFormat.Agent)
        );
        Assert.Equal(string.Empty, Renderer.Render(Report(DocumentationOff(), FellBack()), ReportFormat.Plain));
    }

    /// <summary>
    ///     ⚠ A blocking warning (<c>SK9030</c>, which the reliability gate fails on) is the banner's,
    ///     and is printed once: under the banner, not again in the <c>WARNING</c> block.
    /// </summary>
    [Fact]
    public void ABlockingWarning_IsTheBannersAndIsNotPrintedTwice() {
        var crashed = new SkalaDiagnostic(
            "SK9030",
            SkalaSeverity.Warning,
            "analyzer 'X' threw once, so the rules it carries (SK0232) reported nothing wherever it threw.",
            "X"
        );
        var report = Report(crashed, Unanalysed());

        var agent = Renderer.Render(report, ReportFormat.Agent);
        Assert.StartsWith("INCOMPLETE  ", agent, StringComparison.Ordinal);
        Assert.Contains("WARNING 1 warning about this run", agent, StringComparison.Ordinal);
        Assert.Equal(1, Count(agent, "SK9030  X"));

        var plain = Renderer.Render(report, ReportFormat.Plain);
        Assert.Equal(1, Count(plain, "warning SK9030:"));
        Assert.Equal(1, Count(plain, "warning SK9021:"));
        Assert.Equal([crashed], Renderer.Blocking(report));
        Assert.Equal([Unanalysed()], Renderer.Notices(report));
    }

    [Fact]
    public void Agent_BoundsTheWarningBlock_AndNamesTheCommandForTheRest() {
        var many = Enumerable.Range(0, AgentRenderer.MaxWarnings + 5)
            .Select(index => Unanalysed() with { File = Path.Combine(Root, "build", "F" + index + ".cs") })
            .ToArray();

        var text = Renderer.Render(Report(many), ReportFormat.Agent);

        Assert.Equal(AgentRenderer.MaxWarnings, Count(text, "  SK9021  build/F"));
        Assert.Contains(
            "  … 5 more elided. Run `skala verify --format=json` for all of them.",
            text,
            StringComparison.Ordinal
        );
    }

    static int Count(string text, string needle) {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) {
            count++;
        }

        return count;
    }
}
