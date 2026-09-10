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
    static RunReport Reverted(string? crashPath = null) =>
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
                    $"A reproduction is in {crashPath ?? Crash}. This is a Skala bug; the file was left untouched."
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
    [Theory]
    [InlineData(@"C:\Users\runneradmin\AppData\Local\Temp\skala-345\.skala\crash\9f2a1c")]
    [InlineData("/tmp/skala-345/.skala/crash/9f2a1c")]
    public void Verify_MarksTheSarifInvocationUnsuccessful(string crashPath) {
        var output = Run(ReportFormat.Json, Reverted(crashPath), ExitCodes.InternalError).Output;
        using var document = System.Text.Json.JsonDocument.Parse(output);
        var invocation = document.RootElement.GetProperty("runs")[0].GetProperty("invocations")[0];

        Assert.False(invocation.GetProperty("executionSuccessful").GetBoolean());
        Assert.Equal(ExitCodes.InternalError, invocation.GetProperty("exitCode").GetInt32());

        var notification = invocation.GetProperty("toolExecutionNotifications")[0];
        Assert.Equal("SK9098", notification.GetProperty("descriptor").GetProperty("id").GetString());
        Assert.Contains(
            crashPath,
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
    ///     JSON is checked above after parsing: Windows backslashes are escaped in the document.
    /// </summary>
    [Theory]
    [InlineData(ReportFormat.Agent)]
    [InlineData(ReportFormat.Plain)]
    [InlineData(ReportFormat.Terminal)]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.JUnit)]
    public void Verify_NamesTheCrashReproductionInTextFormats(ReportFormat format) =>
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

    static readonly string Locked = Path.Combine(Root, "Locked.cs");

    /// <summary>
    ///     The report <c>verify</c> builds over one file the process may not read: the per-file
    ///     <c>SK9015</c> from the formatting stage and nothing Skala did wrong.
    /// </summary>
    static RunReport Unreadable() =>
        new() {
            RepositoryRoot = Root,
            Mode = LoadMode.Loose,
            FileCount = 754,
            LineCount = 90_000,
            LoadSummary = "loose (754 file(s), no project)",
            Duration = TimeSpan.FromSeconds(2),
            Diagnostics = [
                new SkalaDiagnostic("SK9015", SkalaSeverity.Error, $"Access to the path '{Locked}' is denied.", Locked)
            ]
        };

    /// <summary>
    ///     ⚠ #355, asserted over the whole of what <c>verify</c> prints — the INCOMPLETE banner, the
    ///     per-file line <em>and</em> the PARTIAL trailer. The banner and the trailer both said "Skala
    ///     bug" over an unreadable file, and the per-file assertion in <c>ExitCodeContractTests</c>
    ///     could not see either. A mode-000 file is fixed with <c>chmod</c>, and a sentence sending
    ///     the reader to look for a defect in the tool spends the banner's credibility on nothing.
    /// </summary>
    [Theory]
    [InlineData(ReportFormat.Agent)]
    [InlineData(ReportFormat.Plain)]
    [InlineData(ReportFormat.Terminal)]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.Github)]
    public void Verify_NeverCallsAnUnreadableFileASkalaBug(ReportFormat format) {
        var result = Run(format, Unreadable(), ExitCodes.InternalError);

        // ⚠ The exit code is deliberately untouched (#353 pinned 5 across all four verbs); only the
        // words change.
        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Contains("Locked.cs", result.Output, StringComparison.Ordinal);
        Assert.Contains("SK9015", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Skala bug", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OK", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The control that keeps the theory above honest: the same two surfaces still say it, in
    ///     the same words, when the cause really is Skala. Delete the sentence everywhere and this
    ///     goes red.
    /// </summary>
    [Fact]
    public void Verify_StillCallsARevertASkalaBug_InBothTheBannerAndTheTrailer() {
        var output = Run(ReportFormat.Agent, Reverted(), ExitCodes.InternalError).Output;

        Assert.Contains("this is a Skala bug, not a finding in your code.", output, StringComparison.Ordinal);
        Assert.Contains("Exit 5 is that Skala bug, not a gate failure.", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The mixed run. A tree can hold a reverted file and an unreadable one at once, and the
    ///     decision is that the banner names both, with a count each, Skala's fault first — a banner
    ///     naming only the first cause is the issue's defect one level down. The trailer keeps the
    ///     word "bug" because one of the causes is one.
    /// </summary>
    [Fact]
    public void Verify_NamesBothCausesWhenARevertAndAnUnreadableFileCoincide() {
        var reverted = Reverted();
        var mixed = reverted with { Diagnostics = [.. reverted.Diagnostics, .. Unreadable().Diagnostics] };

        var output = Run(ReportFormat.Agent, mixed, ExitCodes.InternalError).Output;

        Assert.StartsWith(
            "INCOMPLETE  2 of 754 files were not checked — 1 a Skala bug, not a finding in your code; "
            + "1 could not be read (check permissions and that the path is still mounted).",
            output,
            StringComparison.Ordinal
        );
        Assert.Contains("752 files were checked", output, StringComparison.Ordinal);
        Assert.Contains("2 could not be checked. Exit 5 is that Skala bug", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     What <c>check --gate=local</c> hands <c>verify</c> for a one-file tree whose baseline holds a
    ///     merge-conflict marker, after #358: the error-severity <c>SK9028</c> at the baseline, the gate
    ///     failed on it, no findings, exit 1.
    /// </summary>
    static RunReport ConflictedBaseline() {
        var baseline = Path.Combine(Root, ".skala", "baseline.sarif");
        return new() {
            RepositoryRoot = Root,
            Mode = LoadMode.Loose,
            FileCount = 1,
            LineCount = 8,
            LoadSummary = "loose (1 file(s), no project)",
            Duration = TimeSpan.FromMilliseconds(120),
            Diagnostics = [
                new SkalaDiagnostic(
                    "SK9028",
                    SkalaSeverity.Error,
                    $"the baseline at {baseline} could not be read: {baseline} is not valid JSON",
                    baseline
                )
            ],
            Gate = new GateResult(
                "local",
                false,
                [
                    "1 input(s) the gate compares against could not be read, so this verdict has nothing to call "
                    + "a finding new or accepted against"
                ]
            )
        };
    }

    /// <summary>
    ///     ⚠ #360, measured through the binary before the fix: <c>check --gate=local --format=agent
    ///     --baseline .skala/baseline.sarif</c> over that tree exited 1, and <c>verify</c> with the same
    ///     arguments printed the same INCOMPLETE banner and exited <b>0</b>. <c>Verdict</c> recomputed
    ///     the exit from <c>report.New</c> alone and overruled the gate it had just been handed —
    ///     the banner-above-exit-0 shape #358 had closed for <c>check</c>, open one verb over.
    /// </summary>
    [Theory]
    [InlineData(ReportFormat.Agent)]
    [InlineData(ReportFormat.Plain)]
    [InlineData(ReportFormat.Json)]
    public void Verify_DoesNotOverruleAGateThatFailedOnAnUnreadableBaseline(ReportFormat format) {
        var result = Run(format, ConflictedBaseline(), ExitCodes.GateFailed);

        Assert.Equal(ExitCodes.GateFailed, result.ExitCode);
    }

    /// <summary>
    ///     The same line took exit 3 down to 0. <c>check</c> refuses a positional path that is part of
    ///     no document in the load at <c>ConfigurationError</c>, with an empty report — and an empty
    ///     report is "clean". <c>verify</c> keeps whatever non-zero exit <c>check</c> reached.
    /// </summary>
    [Fact]
    public void Verify_DoesNotTurnARefusalIntoAPass() {
        var refused = ConflictedBaseline() with { Diagnostics = [], Gate = null, FileCount = 0 };

        Assert.Equal(ExitCodes.ConfigurationError, Run(ReportFormat.Agent, refused, ExitCodes.ConfigurationError).ExitCode);
    }

    /// <summary>
    ///     The agent surface prints no gate verdict, so over that tree the banner is the only line
    ///     that explains the exit code — and it has to name the baseline, not the source file.
    /// </summary>
    [Fact]
    public void Verify_NamesTheBaselineAndNotAFile_WhenTheBaselineCouldNotBeRead() {
        var output = Run(ReportFormat.Agent, ConflictedBaseline(), ExitCodes.GateFailed).Output;

        Assert.StartsWith(
            "INCOMPLETE  the baseline at .skala/baseline.sarif could not be read, so the gate compared against nothing.",
            output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("1 of 1", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Skala bug", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OK", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The control for the exit-code claim: <c>verify</c> still adds the stricter direction. A
    ///     gate that passed over a tree with work outstanding is exit 1, and a gate that passed over a
    ///     clean tree is exit 0 — the line this change did not touch.
    /// </summary>
    [Fact]
    public void Verify_StillTightensAPassedGate_InTheStricterDirectionOnly() {
        var clean = ConflictedBaseline() with { Diagnostics = [], Gate = new GateResult("local", true, []) };
        var outstanding = clean with {
            Findings = [
                new Finding {
                    RuleId = "SK1001",
                    Severity = SkalaSeverity.Warning,
                    Message = "use `var`",
                    Path = Path.Combine(Root, "One.cs"),
                    Line = 5
                }
            ]
        };

        Assert.Equal(ExitCodes.Ok, Run(ReportFormat.Agent, clean, ExitCodes.Ok).ExitCode);
        Assert.Equal(ExitCodes.GateFailed, Run(ReportFormat.Agent, outstanding, ExitCodes.Ok).ExitCode);
    }
}
