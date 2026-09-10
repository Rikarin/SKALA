using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #358: a baseline that exists and cannot be read — because it is not JSON, or because this process
///     may not open it — is a repository condition with a stated verdict, not a stack trace and not a
///     clean exit.
/// </summary>
/// <remarks>
///     <para>
///         Measured through the real binary before the fix, over a one-file tree. A
///         <c>.skala/baseline.sarif</c> holding <c>&lt;&lt;&lt;&lt;&lt;&lt;&lt; HEAD</c> made <c>check</c>,
///         <c>report</c> and every <c>baseline</c> verb print
///         <c>skala: internal error — this is a Skala bug.</c> and a
///         <c>Newtonsoft.Json.JsonReaderException</c> stack trace at exit 5. A mode-000 baseline made
///         <c>check</c> print one line — <c>skala: Access to the path … is denied.</c> — at exit 5 with
///         no report at all, from the CLI's outer net.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The issue's "exit 0" half was a claim about the path these two were about to be routed
///             onto, and it held there.
///         </b> A baseline that already reached <c>CheckCommand</c>'s filter —
///         a file holding the literal <c>null</c> — was written as an error-severity <c>SK9028</c>,
///         rendered, and decided on by nothing: the run compared against no baseline, the <c>local</c>
///         gate has no <c>newIssues</c> condition, and the agent renderer printed
///         <c>INCOMPLETE … this is a Skala bug</c> above <b>exit 0</b>. Widening the filter alone would
///         have moved both cases from a loud crash to that. <c>Gate.EvaluateReliability</c> now fails the
///         verdict on the diagnostic, which is what the exit codes here assert.
///     </para>
///     <para>
///         ⚠ <b>Sabotage, in two places because the fix is in two.</b> Remove <c>UnauthorizedAccessException</c>
///         from the filter in <c>CheckCommand.Scope</c> and the mode-000 fixture throws instead of
///         reporting. Remove the <c>JsonException</c> translation in <c>SarifReader.Deserialize</c> and the
///         conflict-marker fixtures throw — the filter never named the type, which is the shape the issue
///         found. Remove the <c>SK9028</c> clause from <c>Gate.EvaluateReliability</c> and both
///         <c>check</c> fixtures exit 0 under a stated error.
///     </para>
///     <para>
///         ⚠ The mode-000 fixture decides by attempting the read (<see cref="Scratch.WriteUnreadable" />):
///         root opens a mode-000 file, and every assertion here would then hold for the wrong reason.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class BaselineReadTests {
    const string Skip = "needs a POSIX mode bit this process is subject to; root and Windows are exempt.";

    const string ConflictMarker = """
                                  {
                                  <<<<<<< HEAD
                                    "version": "2.1.0"
                                  =======
                                    "version": "2.1.0", "runs": []
                                  >>>>>>> other
                                  }
                                  """;

    const string EmptyBaseline = """
                                 {"version":"2.1.0","runs":[{"tool":{"driver":{"name":"skala"}},"results":[]}]}
                                 """;

    const string Source = """
                          namespace Scratch;

                          public sealed class Widget {
                              public int Value { get; set; }
                          }
                          """;

    static CheckRequest Request(Scratch scratch, string baseline) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Loose,
            AllowLoadFallback = false,
            Output = string.Empty,
            IncludeMetrics = false,
            NoCache = true,
            BaselinePath = baseline
        };

    /// <summary>
    ///     What every fixture in this class asserts: the command returned rather than threw, the
    ///     diagnostic names the file, the output does not blame the tool, and the exit is not clean.
    /// </summary>
    static void AssertRefusedWithoutBlame(
        (CommandResult Result, RunReport Report) run,
        string baseline,
        int expectedExit
    ) {
        var diagnostic = Assert.Single(
            run.Report.Diagnostics,
            static diagnostic => diagnostic.Id == ConfigDiagnosticIds.GateInputUnavailable
        );

        Assert.Equal(SkalaSeverity.Error, diagnostic.Severity);
        Assert.Equal(baseline, diagnostic.File);
        Assert.Contains(baseline, diagnostic.Message, StringComparison.Ordinal);

        Assert.DoesNotContain("Skala bug", run.Result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", run.Result.Output, StringComparison.Ordinal);

        Assert.Equal(expectedExit, run.Result.ExitCode);
        Assert.False(run.Report.Gate!.Passed);
        Assert.Contains(
            run.Report.Gate.Failures,
            static failure => failure.Contains("could not be read", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Check_OverAConflictMarkerBaseline_FailsTheGateWithoutAStackTrace() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Source);
        var baseline = scratch.Write(Path.Combine(".skala", "baseline.sarif"), ConflictMarker);

        var run = CheckCommand.Run(Request(scratch, baseline), TestContext.Current.CancellationToken);

        AssertRefusedWithoutBlame(run, baseline, ExitCodes.GateFailed);
        Assert.Contains("is not valid JSON", run.Result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_OverAnUnreadableBaseline_FailsTheGateWithoutAStackTrace() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Source);
        if (scratch.WriteUnreadable(Path.Combine(".skala", "baseline.sarif"), EmptyBaseline) is not { } baseline) {
            Assert.Skip(Skip);
            return;
        }

        var run = CheckCommand.Run(Request(scratch, baseline), TestContext.Current.CancellationToken);

        AssertRefusedWithoutBlame(run, baseline, ExitCodes.GateFailed);
    }

    /// <summary>
    ///     ⚠ The control that keeps the two above from passing for the wrong reason: the same tree
    ///     over a baseline that reads is exit 0, so the failures above are the baseline's and not the
    ///     tree's.
    /// </summary>
    [Fact]
    public void Check_OverAReadableBaseline_StillPasses() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Source);
        var baseline = scratch.Write(Path.Combine(".skala", "baseline.sarif"), EmptyBaseline);

        var (result, report) = CheckCommand.Run(Request(scratch, baseline), TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.True(report.HasBaseline);
        Assert.DoesNotContain(report.Diagnostics, static d => d.Id == ConfigDiagnosticIds.GateInputUnavailable);
    }

    /// <summary>
    ///     ⚠ <c>baseline</c> had no filter at all, and <c>update</c> is the verb a person runs to repair
    ///     the conflicted file — so the repair crashed on the thing it was repairing.
    /// </summary>
    [Theory]
    [InlineData(BaselineCommand.Verb.Show)]
    [InlineData(BaselineCommand.Verb.Update)]
    [InlineData(BaselineCommand.Verb.Create)]
    public void BaselineVerbs_OverAConflictMarkerBaseline_RefuseAndSayHowToRecover(BaselineCommand.Verb verb) {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Source);
        var baseline = scratch.Write(Path.Combine(".skala", "baseline.sarif"), ConflictMarker);

        var (result, _) = BaselineCommand.Run(
            verb,
            Request(scratch, baseline),
            true,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains(baseline, result.Output, StringComparison.Ordinal);
        Assert.Contains("merge-conflict marker", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Skala bug", result.Output, StringComparison.Ordinal);

        // ⚠ `create --apply` over a corrupt file must not have "resolved" the conflict by overwriting it.
        Assert.Equal(ConflictMarker, File.ReadAllText(baseline));
    }

    [Fact]
    public void BaselineShow_OverAnUnreadableBaseline_RefusesWithoutTheConflictHint() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Source);
        if (scratch.WriteUnreadable(Path.Combine(".skala", "baseline.sarif"), EmptyBaseline) is not { } baseline) {
            Assert.Skip(Skip);
            return;
        }

        var (result, _) = BaselineCommand.Run(
            BaselineCommand.Verb.Show,
            Request(scratch, baseline),
            false,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains(baseline, result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("merge-conflict marker", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Skala bug", result.Output, StringComparison.Ordinal);
    }

    /// <summary><c>report</c> reads a SARIF through the same deserialiser and had the same filter.</summary>
    [Fact]
    public void Report_OverAConflictMarkerSarif_ExitsConfigurationError() {
        using var scratch = new Scratch();
        var sarif = scratch.Write("report.sarif", ConflictMarker);

        var result = ReportCommand.Run(sarif, scratch.Root, ReportFormat.Plain, false, false);

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains(sarif, result.Output, StringComparison.Ordinal);
        Assert.Contains("is not valid JSON", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_OverAnUnreadableSarif_ExitsConfigurationError() {
        using var scratch = new Scratch();
        if (scratch.WriteUnreadable("report.sarif", EmptyBaseline) is not { } sarif) {
            Assert.Skip(Skip);
            return;
        }

        var result = ReportCommand.Run(sarif, scratch.Root, ReportFormat.Plain, false, false);

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains(sarif, result.Output, StringComparison.Ordinal);
    }
}
