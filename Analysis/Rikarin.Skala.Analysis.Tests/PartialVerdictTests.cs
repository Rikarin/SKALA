using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #345: <c>skala verify</c> printed <c>OK  nothing to do.</c> and exited 5.
/// </summary>
/// <remarks>
///     <para>
///         The measured shipping behaviour, over the issue's own reproduction (a file that trips the
///         arrangement safety layer, run through a scratch project so the semantic half is live):
///         <c>agent</c> emitted <c>"OK  nothing to do.\n"</c> and nothing else — 19 bytes, and
///         <c>OK  nothing to do.</c> is the exact string the contract reserves for exit 0. <c>plain</c>
///         emitted <b>zero bytes</b>. <c>json</c> emitted 542 KB. All three exited 5, and
///         <c>.skala/crash/</c> had been written in all three.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The issue's account of the <c>json</c> format is wrong and this is worth as much as the
///             fix.
///         </b> "Only <c>--format json</c> carries the <c>SK9098</c>, the *This is a Skala bug; the
///         file was left untouched* sentence, and the path to the crash reproduction" held for the id
///         and the message only. The SARIF notification carried neither
///         <see cref="SkalaDiagnostic.File" /> nor <see cref="SkalaDiagnostic.Detail" />, so in that
///         542 KB document the failing file's name appeared <b>zero times</b> and the crash directory
///         never — <em>no</em> format carried either. The single <c>.skala/crash/</c> string in it was
///         <c>SK9099</c>'s boilerplate rule help, about a different diagnostic entirely. That SARIF also
///         said <c>"executionSuccessful": true</c>.
///     </para>
///     <para>
///         ⚠ These drive <see cref="VerifyCommand.Verdict" /> rather than <see cref="VerifyCommand.Run" />
///         <b>on purpose</b>. An end-to-end fixture needs an arrangement rule that actually fails, and
///         every such failure is a bug somebody is fixing — the issue's own reproduction is #341's, and
///         ten deliberately hostile candidates (target-typed <c>new</c> and <c>default</c> under an
///         inferred type, <c>this.</c>/<c>Holder.</c> qualifiers over a shadowing parameter or local, a
///         <c>cref</c>-only using, a shadowed <c>Int32</c>) were measured against
///         <c>csharp_style_var_* = true</c> and every one of them was correctly declined. So there is no
///         source that forces this and will keep forcing it, and a fixture built on one that does is a
///         hostage to its fix. This is the same decision on the same path, with the report built by
///         hand.
///     </para>
/// </remarks>
public sealed class PartialVerdictTests {
    static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "skala-345"));
    static readonly string Failed = Path.Combine(Root, "Report.cs");
    static readonly string Crash = Path.Combine(Root, ".skala", "crash", "9f2a1c");

    /// <summary>
    ///     A report shaped exactly as <see cref="ArrangementFindings" /> leaves one after a revert: the
    ///     per-file <c>SK9098</c> carrying the crash path in its detail, then the stage summary.
    /// </summary>
    static RunReport Reverted() =>
        new() {
            RepositoryRoot = Root,
            Mode = LoadMode.Workspace,
            FileCount = 754,
            LineCount = 90_000,
            LoadSummary = "workspace (1 project)",
            Duration = TimeSpan.FromSeconds(9),
            Diagnostics = [
                new SkalaDiagnostic(
                    "SK9098",
                    SkalaSeverity.Error,
                    "not arranged, re-binding the rewritten document produced 2 diagnostic(s) it did not "
                    + "have before: CS0128, CS0128",
                    Failed,
                    0,
                    $"A reproduction is in {Crash}. This is a Skala bug; the file was left untouched."
                ),
                new SkalaDiagnostic(
                    "SK9015",
                    SkalaSeverity.Error,
                    "the arrange stage could not inspect every requested file; the files it names above "
                    + "were left exactly as they were and are not covered by this report",
                    Root,
                    0,
                    "Report.cs  error SK9098: not arranged"
                )
            ]
        };

    static CommandResult Run(ReportFormat format, RunReport report, int exitCode) =>
        VerifyCommand.Verdict(format, new CommandResult(exitCode, Renderer.Render(report, format)), report);

    /// <summary>
    ///     ⚠ The three assertions the issue asked for, per format: the exit code is still 5, the output
    ///     says neither <c>OK</c> nor <c>nothing to do</c>, and it names the file that was not checked.
    /// </summary>
    [Theory]
    [InlineData(ReportFormat.Agent)]
    [InlineData(ReportFormat.Plain)]
    public void Verify_NeverPrintsACleanVerdictOnAnInternalError(ReportFormat format) {
        var result = Run(format, Reverted(), ExitCodes.InternalError);

        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.DoesNotContain("OK", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing to do", result.Output, StringComparison.Ordinal);
        Assert.Contains("Report.cs", result.Output, StringComparison.Ordinal);
        Assert.Contains("SK9098", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The same claim for <c>json</c>, asked of the invocation rather than of the document.
    /// </summary>
    /// <remarks>
    ///     ⚠ The literal "output contains neither <c>OK</c> nor <c>nothing to do</c>" cannot be asked
    ///     of this format and it is not a weaker fixture to say so: the SARIF embeds the whole rule
    ///     catalogue — every rule that *could* fire, per docs/plan/09 — and one rule's help text happens
    ///     to contain the phrase. Asserting over 542 KB of boilerplate would have been asserting about
    ///     `rules[]`, not about the verdict. SARIF states a verdict in exactly one place, and this is
    ///     it.
    ///     <para>
    ///         ⚠ <c>executionSuccessful</c> was <c>true</c> on the reproduction, measured. It read
    ///         <c>!report.Partial</c>, and <see cref="RunReport.Partial" /> is set only by a cancelled or
    ///         failed *analyzer* — arrangement, the token-equivalence check and an unreadable file all
    ///         leave it false. The one field SARIF has for "did this run complete" said yes.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Verify_MarksTheSarifInvocationUnsuccessful() {
        var output = Run(ReportFormat.Json, Reverted(), ExitCodes.InternalError).Output;
        using var document = System.Text.Json.JsonDocument.Parse(output);
        var invocation = document.RootElement.GetProperty("runs")[0].GetProperty("invocations")[0];

        Assert.False(invocation.GetProperty("executionSuccessful").GetBoolean());
        Assert.Equal(ExitCodes.InternalError, invocation.GetProperty("exitCode").GetInt32());

        var notification = invocation.GetProperty("toolExecutionNotifications")[0];
        Assert.Equal("SK9098", notification.GetProperty("descriptor").GetProperty("id").GetString());
        Assert.Contains(
            Crash,
            notification.GetProperty("message").GetProperty("text").GetString(),
            StringComparison.Ordinal
        );

        // ⚠ The failing file's name appeared zero times in the shipping SARIF: a notification carried
        // no location at all, so the one format that "carried the SK9098" could not say which file.
        Assert.Equal(
            "Report.cs",
            notification.GetProperty("locations")[0]
                .GetProperty("physicalLocation")
                .GetProperty("artifactLocation")
                .GetProperty("uri")
                .GetString()
        );
    }

    /// <summary>
    ///     ⚠ The crash directory is the one thing that lets a reader act on this, and before #345 it
    ///     was printed by no surface at all: it lives in <see cref="SkalaDiagnostic.Detail" />, which
    ///     the terminal renderer skipped, the plain and agent renderers never looked at, and the SARIF
    ///     writer dropped. #345 was found by noticing <c>.skala/crash/</c> on disk.
    /// </summary>
    [Theory]
    [InlineData(ReportFormat.Agent)]
    [InlineData(ReportFormat.Plain)]
    [InlineData(ReportFormat.Json)]
    [InlineData(ReportFormat.Terminal)]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.JUnit)]
    public void Verify_NamesTheCrashReproductionInEveryFormat(ReportFormat format) =>
        Assert.Contains(Crash, Run(format, Reverted(), ExitCodes.InternalError).Output, StringComparison.Ordinal);

    /// <summary>
    ///     ⚠ Every format says the run did not finish, and names the file it did not finish on.
    ///     <c>github</c> and <c>terminal</c> always did; the other five did not, in five different
    ///     ways, which is what makes this a property of the report rather than of one renderer.
    /// </summary>
    [Theory]
    [InlineData(ReportFormat.Agent)]
    [InlineData(ReportFormat.Plain)]
    [InlineData(ReportFormat.Json)]
    [InlineData(ReportFormat.Terminal)]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.JUnit)]
    [InlineData(ReportFormat.Github)]
    public void Verify_NamesTheStageAndTheFileInEveryFormat(ReportFormat format) {
        var output = Run(format, Reverted(), ExitCodes.InternalError).Output;

        Assert.Contains("SK9098", output, StringComparison.Ordinal);
        Assert.Contains("Report.cs", output, StringComparison.Ordinal);
        Assert.Contains("the arrange stage", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ JUnit counts, specifically. A CI system whose only report surface is a test result reads
    ///     the attributes and nothing else, so an incomplete run has to arrive as failing cases — a
    ///     green suite over a run that could not read part of the tree is the <c>plain</c> defect
    ///     wearing a schema.
    /// </summary>
    [Fact]
    public void Verify_CountsAnIncompleteRunAsAJUnitFailure() {
        var output = Run(ReportFormat.JUnit, Reverted(), ExitCodes.InternalError).Output;

        Assert.Contains("<testsuites name=\"Skala\" tests=\"2\" failures=\"2\"", output, StringComparison.Ordinal);
        Assert.Contains("skala.incomplete", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The partial verdict, and the answer to the question the issue asked. One file's
    ///     <c>SK9098</c> does <b>not</b> take the run's verdict down — the other 753 were arranged and
    ///     checked, and saying so is the difference between an exit 5 worth reading and the weeks in
    ///     which nobody could tell that the <c>check</c> gate underneath it had its own verdict.
    /// </summary>
    [Fact]
    public void Verify_SaysHowMuchOfTheTreeTheVerdictCovers() {
        var output = Run(ReportFormat.Agent, Reverted(), ExitCodes.InternalError).Output;

        Assert.Contains("753 files were checked", output, StringComparison.Ordinal);
        Assert.Contains("1 could not be checked", output, StringComparison.Ordinal);
        Assert.Contains("not a gate failure", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Still 5, and still not 1. The issue asked whether a partial verdict should exit like a
    ///     gate failure instead; it must not. A repository's own lint debt and a bug in Skala are the
    ///     one pair of outcomes an exit code has to keep apart — a pre-commit hook that cannot tell
    ///     them apart either retries a broken tool for ever or accepts a tree it never checked.
    /// </summary>
    [Fact]
    public void Verify_KeepsASkalaBugDistinctFromAGateFailure() {
        var report = Reverted();

        Assert.Equal(ExitCodes.InternalError, Run(ReportFormat.Agent, report, ExitCodes.InternalError).ExitCode);
        Assert.NotEqual(ExitCodes.GateFailed, Run(ReportFormat.Agent, report, ExitCodes.InternalError).ExitCode);

        // The same report with no blocking diagnostic is an ordinary clean run, and stays exit 0.
        var healthy = report with { Diagnostics = [] };
        Assert.Equal(ExitCodes.Ok, Run(ReportFormat.Agent, healthy, ExitCodes.Ok).ExitCode);
        Assert.Equal("OK  nothing to do.\n", Run(ReportFormat.Agent, healthy, ExitCodes.Ok).Output);
    }

    /// <summary>
    ///     ⚠ <c>json</c> and <c>junit</c> get the facts structurally and no prose: a verdict line
    ///     appended to either is a corrupt document, not a clearer one.
    /// </summary>
    [Theory]
    [InlineData(ReportFormat.Json)]
    [InlineData(ReportFormat.JUnit)]
    public void Verify_DoesNotAppendProseToAParsedDocument(ReportFormat format) =>
        Assert.DoesNotContain(
            "PARTIAL",
            Run(format, Reverted(), ExitCodes.InternalError).Output,
            StringComparison.Ordinal
        );

    /// <summary>
    ///     ⚠ A load failure is not a partial verdict. Nothing was analysed, so there is no "the rest of
    ///     the tree was fine" to report; the output stays what <c>CheckCommand</c> wrote and the exit
    ///     code stays 4.
    /// </summary>
    [Fact]
    public void Verify_LeavesALoadFailureAlone() {
        var result = Run(
            ReportFormat.Agent,
            new RunReport { RepositoryRoot = Root, Mode = LoadMode.Loose },
            ExitCodes.LoadFailure
        );

        Assert.Equal(ExitCodes.LoadFailure, result.ExitCode);
        Assert.DoesNotContain("PARTIAL", result.Output, StringComparison.Ordinal);
    }
}
