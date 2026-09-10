using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
///     #355: the <c>INCOMPLETE</c> banner said "this is a Skala bug" over a file Skala was simply not
///     allowed to read.
/// </summary>
/// <remarks>
///     <para>
///         The contradiction was already half-asserted before this class existed.
///         <c>ExitCodeContractTests.Five_WhenAFileCannotBeRead</c> asserts that the per-file
///         <c>SK9015</c> line is not reported as a Skala bug, and the banner one line above it said
///         exactly that — no test read the two together, which is how they drifted. Every assertion
///         here is over the <b>whole</b> rendered output, so the banner, the per-file line and anything
///         a future surface adds are held to the same sentence at once.
///     </para>
///     <para>
///         ⚠ The genuine-defect case is asserted positively, and that is what stops this class passing
///         by deletion: a fix that removed the sentence everywhere would satisfy every "does not say
///         bug" assertion and fail <see cref="AgentBanner_StillCallsATokenStreamFailureASkalaBug" />.
///     </para>
/// </remarks>
public sealed class IncompleteBannerTests {
    /// <summary>⚠ One constant: <c>SK7083</c>'s threshold is five literals per file.</summary>
    const string SkalaBug = "Skala bug";

    static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "skala-355"));
    static readonly string Locked = Path.Combine(Root, "Locked.cs");
    static readonly string Refused = Path.Combine(Root, "Refused.cs");
    static readonly string Broken = Path.Combine(Root, "Broken.cs");

    /// <summary>Every surface that renders prose. <c>json</c> and <c>junit</c> are documents, not text.</summary>
    public static TheoryData<ReportFormat> TextFormats =>
        new(ReportFormat.Agent, ReportFormat.Plain, ReportFormat.Terminal, ReportFormat.Markdown, ReportFormat.Github);

    static RunReport Report(params SkalaDiagnostic[] diagnostics) =>
        new() {
            RepositoryRoot = Root,
            Mode = LoadMode.Loose,
            FileCount = 4,
            LineCount = 80,
            LoadSummary = "loose (4 file(s), no project)",
            Duration = TimeSpan.FromMilliseconds(300),
            Diagnostics = [.. diagnostics]
        };

    /// <summary>Exactly what <c>FormattingFindings</c> and the loose loader emit for a mode-000 file.</summary>
    static SkalaDiagnostic Unreadable(string path = "") =>
        new(
            "SK9015",
            SkalaSeverity.Error,
            $"Access to the path '{(path.Length == 0 ? Locked : path)}' is denied.",
            path.Length == 0 ? Locked : path
        );

    /// <summary>The formatter's safety net, detail and all.</summary>
    static SkalaDiagnostic TokenStreamChanged(string path = "") =>
        new(
            "SK9099",
            SkalaSeverity.Error,
            "not written, the formatted output has a different token stream",
            path.Length == 0 ? Refused : path,
            0,
            $"A reproduction is in {Path.Combine(Root, ".skala", "crash", "1a2b3c")}. "
            + "This is a Skala bug; the file was left untouched."
        );

    /// <summary>
    ///     ⚠ The stage summary <c>ArrangementFindings</c> appends after <em>any</em> arrangement
    ///     failure: located at the root and carrying <c>SK9015</c> whatever went wrong upstream.
    /// </summary>
    static SkalaDiagnostic ArrangeStageSummary() =>
        new(
            "SK9015",
            SkalaSeverity.Error,
            "the arrange stage could not inspect every requested file; the files it names above were "
            + "left exactly as they were and are not covered by this report",
            Root,
            0,
            "Refused.cs  error SK9099: not written"
        );

    /// <summary>
    ///     ⚠ The pair, asserted together, on every text surface: the file is named, the id is
    ///     <c>SK9015</c>, and nothing in the output — banner included — calls it a Skala bug.
    /// </summary>
    [Theory]
    [MemberData(nameof(TextFormats))]
    public void AnUnreadableFile_IsNeverCalledASkalaBug(ReportFormat format) {
        var text = Renderer.Render(Report(Unreadable()), format);

        Assert.Contains("Locked.cs", text, StringComparison.Ordinal);
        Assert.Contains("SK9015", text, StringComparison.Ordinal);
        Assert.DoesNotContain(SkalaBug, text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The banner says what it is instead, and says what to check.</summary>
    [Fact]
    public void AgentBanner_SaysUnreadableAndWhatToCheck() {
        var text = Renderer.Render(Report(Unreadable()), ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  1 of 4 file was not checked — could not be read: check permissions and that the path "
            + "is still mounted.",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("Everything below covers the rest.", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The control. Without this, deleting the sentence everywhere passes the class.
    /// </summary>
    [Fact]
    public void AgentBanner_StillCallsATokenStreamFailureASkalaBug() {
        var text = Renderer.Render(Report(TokenStreamChanged()), ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  1 of 4 file was not checked — this is a Skala bug, not a finding in your code.",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("SK9099", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The mixed run: every cause named, each with its own count, Skala's own first — and the
    ///     counts add up to the fraction the line opens with.
    /// </summary>
    [Fact]
    public void AgentBanner_NamesEveryCauseInAMixedRun() {
        var report = Report(
            Unreadable(),
            Unreadable(Path.Combine(Root, "AlsoLocked.cs")),
            TokenStreamChanged(),
            ArrangeStageSummary()
        );
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  3 of 4 files were not checked — 1 a Skala bug, not a finding in your code; "
            + "2 could not be read (check permissions and that the path is still mounted). "
            + "Everything below covers the rest.",
            text,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ The trap a naive by-id split falls into. The arrange stage's summary diagnostic is an
    ///     <c>SK9015</c> located at the repository root and is emitted after <em>every</em> kind of
    ///     arrangement failure. Read it as "a file could not be read" and every <c>SK9098</c> in the
    ///     tree becomes a permissions problem. Only per-file diagnostics decide the cause.
    /// </summary>
    [Fact]
    public void TheArrangeStageSummary_DoesNotTurnADefectIntoAPermissionsProblem() {
        var text = Renderer.Render(Report(TokenStreamChanged(), ArrangeStageSummary()), ReportFormat.Agent);

        Assert.Contains("this is a Skala bug, not a finding in your code.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be read", text, StringComparison.Ordinal);
        Assert.DoesNotContain("permissions", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A file carrying two blocking diagnostics is attributed once, to the stronger cause, so the
    ///     per-cause counts sum to the number of files and not to the number of diagnostics.
    /// </summary>
    [Fact]
    public void Causes_AttributeAFileOnce_ToItsStrongestCause() {
        var report = Report(Unreadable(Refused), TokenStreamChanged(Refused), Unreadable(), ArrangeStageSummary());

        var causes = Renderer.Causes(report);

        Assert.Equal([(IncompleteCause.Defect, 1), (IncompleteCause.Unreadable, 1)], causes);
        Assert.Equal(2, Renderer.BlockedFiles(report).Count());
    }

    /// <summary>
    ///     ⚠ Only root-scoped diagnostics leaves nothing to read a cause from, and the answer is the
    ///     conservative one — the pre-#355 sentence — rather than a guess at the environment.
    /// </summary>
    [Fact]
    public void Causes_FallBackToDefect_WhenOnlyTheStageSummaryBlocked() {
        Assert.Equal([(IncompleteCause.Defect, 0)], Renderer.Causes(Report(ArrangeStageSummary())));
    }

    /// <summary>
    ///     ⚠ Anything the classifier has not been told about is Skala's fault. A new blocking id must
    ///     not ship quietly telling the reader to check file permissions.
    /// </summary>
    [Theory]
    [InlineData("SK9098")]
    [InlineData("SK9096")]
    [InlineData("SK9095")]
    [InlineData("SK9097")]
    [InlineData("SK9099")]
    [InlineData("SK9031")]
    public void CauseOf_DefaultsToDefect(string id) =>
        Assert.Equal(
            IncompleteCause.Defect,
            Renderer.CauseOf(new SkalaDiagnostic(id, SkalaSeverity.Error, "m", Broken))
        );

    /// <summary>
    ///     <c>SK9010</c> ships at warning severity and so never reaches the banner today; if it ever
    ///     does, it has its own contract (ADR-003) and the banner follows it rather than calling a
    ///     syntax error a Skala bug.
    /// </summary>
    [Fact]
    public void AgentBanner_FollowsTheNotParseableContract() {
        var report = Report(
            new SkalaDiagnostic("SK9010", SkalaSeverity.Error, "not formatted, the file does not parse", Broken, 3)
        );
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  1 of 4 file was not checked — unparseable: fix the syntax error;",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("this is a Skala bug", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     #356: the fraction survives more blocked files than readable ones, because every blocked
    ///     source file is now in <c>FileCount</c>.
    /// </summary>
    /// <remarks>
    ///     Before the loaders counted a requested file whether or not it opened, a three-file tree
    ///     with two unreadable files reached the renderer as <c>FileCount</c> 1 and <c>blocked</c> 2,
    ///     and <c>Scale</c> printed <c>2 files were not checked</c> with no fraction — the arithmetic
    ///     hidden rather than fixed. This is the report the loaders now build for that tree.
    /// </remarks>
    [Fact]
    public void AgentBanner_KeepsTheFractionWhenMostOfTheTreeWasUnreadable() {
        var report = Report(Unreadable(), Unreadable(Broken)) with { FileCount = 3 };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  2 of 3 files were not checked — could not be read",
            text,
            StringComparison.Ordinal
        );
    }

    static readonly string BaselinePath = Path.Combine(Root, ".skala", "baseline.sarif");

    /// <summary>
    ///     Exactly what <c>CheckCommand.Scope</c> emits for a baseline that exists and will not open —
    ///     a merge-conflict marker, here — after #358: error severity, located at the baseline.
    /// </summary>
    static SkalaDiagnostic ConflictedBaseline() =>
        new(
            "SK9028",
            SkalaSeverity.Error,
            $"the baseline at {BaselinePath} could not be read: {BaselinePath} is not valid JSON: "
            + "Unexpected character encountered while parsing value: <. Path '', line 0, position 0.",
            BaselinePath
        );

    /// <summary>The same id at warning: the gate names a baseline and there is no such file yet.</summary>
    static SkalaDiagnostic AbsentBaseline() =>
        new(
            "SK9028",
            SkalaSeverity.Warning,
            "the gate names a baseline at .skala/baseline.sarif and there is no such file, so every finding "
            + "counts as new. `skala baseline create --apply` writes one.",
            BaselinePath
        );

    /// <summary>
    ///     ⚠ #360, the issue's own shape: a one-file tree and a baseline holding a merge-conflict marker.
    ///     The banner used to read <c>1 of 1 file was not checked — this is a Skala bug</c>. The file
    ///     was checked; the baseline is the repository's; and the banner is the only line on this
    ///     surface that explains the exit code, because <c>agent</c> prints no gate verdict.
    /// </summary>
    [Fact]
    public void AgentBanner_NamesAConflictedBaseline_AndDoesNotCountItAsAFile() {
        var report = Report(ConflictedBaseline()) with { FileCount = 1 };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  the baseline at .skala/baseline.sarif could not be read, so the gate compared against "
            + "nothing. Every file was checked; everything below is shown as if there were nothing to compare "
            + "against.",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(SkalaBug, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 of 1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("was not checked", text, StringComparison.Ordinal);
        Assert.DoesNotContain("did not finish", text, StringComparison.Ordinal);
        Assert.Contains("SK9028  .skala/baseline.sarif", text, StringComparison.Ordinal);
    }

    /// <summary>The same pair on every text surface: the baseline is named and nothing blames the tool.</summary>
    [Theory]
    [MemberData(nameof(TextFormats))]
    public void AConflictedBaseline_IsNeverCalledASkalaBug(ReportFormat format) {
        var text = Renderer.Render(Report(ConflictedBaseline()) with { FileCount = 1 }, format);

        Assert.Contains("baseline.sarif", text, StringComparison.Ordinal);
        Assert.Contains("SK9028", text, StringComparison.Ordinal);
        Assert.DoesNotContain(SkalaBug, text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     The source file's own findings still render under the banner — the run was not partial and
    ///     the report is not to be read as one.
    /// </summary>
    [Fact]
    public void AgentBanner_OverAConflictedBaseline_StillRendersTheSourceFilesFindings() {
        var report = Report(ConflictedBaseline()) with {
            FileCount = 1,
            Findings = [
                new Finding {
                    RuleId = "SK1001",
                    Severity = SkalaSeverity.Warning,
                    Message = "use `var`",
                    Path = Path.Combine(Root, "One.cs"),
                    Line = 5,
                    Column = 9
                }
            ]
        };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.Contains("ACTION  1 finding needs a decision", text, StringComparison.Ordinal);
        Assert.Contains("SK1001  One.cs:5  use `var`", text, StringComparison.Ordinal);
        Assert.True(
            text.IndexOf("INCOMPLETE", StringComparison.Ordinal) < text.IndexOf("ACTION", StringComparison.Ordinal),
            text
        );
    }

    /// <summary>
    ///     ⚠ Mixed: a genuine <c>SK9099</c> and a conflicted baseline over a two-file tree. Both are
    ///     named; the fraction counts the source file only, and the baseline is its own sentence
    ///     outside the <c>N of M</c> arithmetic.
    /// </summary>
    [Fact]
    public void AgentBanner_KeepsTheBaselineOutOfTheFraction_InAMixedRun() {
        var report = Report(TokenStreamChanged(), ConflictedBaseline()) with { FileCount = 2 };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  1 of 2 file was not checked — this is a Skala bug, not a finding in your code. "
            + "The baseline at .skala/baseline.sarif could not be read, so the gate compared against nothing. "
            + "Everything below covers the rest, shown as if there were nothing to compare against.",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("SK9099  Refused.cs", text, StringComparison.Ordinal);
        Assert.Contains("SK9028  .skala/baseline.sarif", text, StringComparison.Ordinal);
        Assert.Equal(Refused, Assert.Single(Renderer.BlockedFiles(report)));
        Assert.Equal([(IncompleteCause.Defect, 1)], Renderer.Causes(report));
    }

    /// <summary>
    ///     ⚠ #358 left the absent baseline non-blocking on purpose, and
    ///     <c>MissingGateInput_DoesNotFailTheReliabilityGate</c> pins that at the gate. This pins it at
    ///     the banner: warning severity never reaches it.
    /// </summary>
    [Fact]
    public void AgentBanner_IsSilentForABaselineThatDoesNotExistYet() {
        var text = Renderer.Render(Report(AbsentBaseline()) with { FileCount = 1 }, ReportFormat.Agent);

        Assert.StartsWith("OK  nothing to do.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("INCOMPLETE", text, StringComparison.Ordinal);
        Assert.Empty(Renderer.GateInputs(Report(AbsentBaseline())));
    }

    /// <summary>
    ///     The root-located variants of the same id — <c>--since</c> that will not resolve,
    ///     <c>--no-new-suppressions</c> that could not compare — fail the same gate clause and used to
    ///     print <c>this run did not finish — this is a Skala bug</c> above it.
    /// </summary>
    [Fact]
    public void AgentBanner_NamesAnUnresolvableSinceReference_WithoutBlamingTheTool() {
        var report = Report(
            new SkalaDiagnostic(
                "SK9028",
                SkalaSeverity.Error,
                "--since=origin/nowhere could not be resolved: fatal: bad revision 'origin/nowhere'",
                Root
            )
        );
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  an input the gate scopes by could not be read (SK9028 below), so the gate compared "
            + "against nothing. Every file was checked;",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(SkalaBug, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("did not finish", text, StringComparison.Ordinal);
        Assert.Contains("--since=origin/nowhere", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #356's "only way in" is closed. <c>Scale</c> kept its <c>FileCount &lt; blocked</c> branch
    ///     because <c>SK9028</c> at the baseline reached it with <c>FileCount</c> 0 and blocked 1; a gate
    ///     input is no longer a blocked file, so over a generated-only tree the same report has no
    ///     fraction to omit — and no "1 file" that was never a file.
    /// </summary>
    [Fact]
    public void AGateInput_IsNotABlockedFile_EvenOverAnEmptyTree() {
        var report = Report(ConflictedBaseline()) with { FileCount = 0 };

        Assert.Empty(Renderer.BlockedFiles(report));
        Assert.Equal(IncompleteCause.GateInput, Renderer.CauseOf(ConflictedBaseline()));
        Assert.Single(Renderer.GateInputs(report));

        var text = Renderer.Render(report, ReportFormat.Agent);
        Assert.DoesNotContain("file was not checked", text, StringComparison.Ordinal);
        Assert.DoesNotContain(" of 0 ", text, StringComparison.Ordinal);
    }

    static readonly string Project = Path.Combine(Root, "Broken.csproj");

    /// <summary>
    ///     Exactly what <c>WorkspaceLoader.LoadCore</c> emits for a <c>.csproj</c> whose SDK does not exist.
    /// </summary>
    static SkalaDiagnostic ProjectThatWouldNotLoad() =>
        new(
            "SK9024",
            SkalaSeverity.Error,
            $"'{Project}' yielded no analysable source; every project in it failed to load",
            Project
        );

    /// <summary>The #336 refusal: the project's generators are not on disk, so the load was refused.</summary>
    static SkalaDiagnostic ProjectWhoseGeneratorsAreMissing() =>
        new(
            "SK9029",
            SkalaSeverity.Error,
            "refusing to analyse 'Broken.csproj': the assemblies above are missing, so the generated half "
            + "of the program is absent and every semantic answer over this load is unsound.",
            Project
        );

    /// <summary>
    ///     The same id at warning: MSBuild's own line, relayed verbatim. This repository prints three
    ///     of them on every workspace load and #336 was first blamed on them.
    /// </summary>
    static SkalaDiagnostic RelayedWorkspaceLine() =>
        new(
            "SK9024",
            SkalaSeverity.Warning,
            "workspace: Found project reference without a matching metadata reference: Other.csproj",
            Project
        );

    /// <summary>
    ///     ⚠ #361, the issue's own shape: <c>One.cs</c> beside a <c>.csproj</c> naming an SDK that does
    ///     not exist, <c>check --load=binlog</c> with no binlog. The workspace rung fails, the ladder
    ///     keeps its error-severity <c>SK9024</c> at the <c>.csproj</c> and falls through to loose, and
    ///     the banner read <c>1 of 1 file was not checked — this is a Skala bug</c> above exit 0. The
    ///     file was checked; the project is what would not load; and the sentence has to say where
    ///     the rules went, because the gate now fails on this and the <c>agent</c> surface prints no
    ///     verdict.
    /// </summary>
    [Fact]
    public void AgentBanner_NamesAProjectThatWouldNotLoad_AndDoesNotCountItAsAFile() {
        var report = Report(ProjectThatWouldNotLoad()) with { FileCount = 1 };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  Broken.csproj could not be loaded, so the run fell back to loose and the rules that "
            + "need a compilation did not run. Every file was checked by the rules that could run; the SKIPPED "
            + "line names the rules that did not run.",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(SkalaBug, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 of 1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("was not checked", text, StringComparison.Ordinal);
        Assert.DoesNotContain("did not finish", text, StringComparison.Ordinal);
        Assert.Contains("SK9024  Broken.csproj", text, StringComparison.Ordinal);
    }

    /// <summary>The same pair on every text surface: the project is named and nothing blames the tool.</summary>
    [Theory]
    [MemberData(nameof(TextFormats))]
    public void AProjectThatWouldNotLoad_IsNeverCalledASkalaBug(ReportFormat format) {
        var text = Renderer.Render(Report(ProjectThatWouldNotLoad()) with { FileCount = 1 }, format);

        Assert.Contains("Broken.csproj", text, StringComparison.Ordinal);
        Assert.Contains("SK9024", text, StringComparison.Ordinal);
        Assert.DoesNotContain(SkalaBug, text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     <c>SK9029</c> is the other id the workspace rung refuses on (#336), located at the same
    ///     <c>.csproj</c>, and takes the same sentence.
    /// </summary>
    [Fact]
    public void AgentBanner_TreatsARefusedLoadTheSameAsAFailedOne() {
        var report = Report(ProjectWhoseGeneratorsAreMissing()) with { FileCount = 1 };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.Equal(IncompleteCause.LoadRung, Renderer.CauseOf(ProjectWhoseGeneratorsAreMissing()));
        Assert.StartsWith(
            "INCOMPLETE  Broken.csproj could not be loaded, so the run fell back to loose",
            text,
            StringComparison.Ordinal
        );
        Assert.Empty(Renderer.BlockedFiles(report));
        Assert.Contains("SK9029  Broken.csproj", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The root-located variant — several <c>.csproj</c> and no <c>--project</c> — used to print
    ///     <c>this run did not finish — this is a Skala bug</c> above exit 0, over a message that is an
    ///     instruction to pass <c>--project</c>. Measured through the binary on 2026-09-11.
    /// </summary>
    [Fact]
    public void AgentBanner_NamesAnAmbiguousWorkspaceTarget_WithoutBlamingTheTool() {
        var report = Report(
            new SkalaDiagnostic(
                "SK9024",
                SkalaSeverity.Error,
                "multiple '*.csproj' workspace targets were found; choose one with --project: A.csproj, B.csproj",
                Root
            )
        );
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  no project could be loaded (SK9024 below), so the run fell back to loose and the rules "
            + "that need a compilation did not run. Every file was checked by the rules that could run;",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(SkalaBug, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("did not finish", text, StringComparison.Ordinal);
        Assert.Contains("choose one with --project", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The source file's own findings still render under the banner: the syntactic half was
    ///     delivered, and the report is to be read as that half.
    /// </summary>
    [Fact]
    public void AgentBanner_OverAProjectThatWouldNotLoad_StillRendersTheSourceFilesFindings() {
        var report = Report(ProjectThatWouldNotLoad()) with {
            FileCount = 1,
            Findings = [
                new Finding {
                    RuleId = "SK6030",
                    Severity = SkalaSeverity.Warning,
                    Message = "`D` is declared in the global namespace",
                    Path = Path.Combine(Root, "One.cs"),
                    Line = 1,
                    Column = 14
                }
            ]
        };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.Contains("ACTION  1 finding needs a decision", text, StringComparison.Ordinal);
        Assert.Contains("SK6030  One.cs:1", text, StringComparison.Ordinal);
        Assert.True(
            text.IndexOf("INCOMPLETE", StringComparison.Ordinal) < text.IndexOf("ACTION", StringComparison.Ordinal),
            text
        );
    }

    /// <summary>
    ///     ⚠ Mixed: a genuine <c>SK9099</c> and a project that would not load over a two-file tree. The
    ///     fraction counts the source file only; the project is its own sentence, and the trailer says
    ///     both what the rest is covered by and where the missing rules are named.
    /// </summary>
    [Fact]
    public void AgentBanner_KeepsTheProjectOutOfTheFraction_InAMixedRun() {
        var report = Report(TokenStreamChanged(), ProjectThatWouldNotLoad()) with { FileCount = 2 };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  1 of 2 file was not checked — this is a Skala bug, not a finding in your code. "
            + "Broken.csproj could not be loaded, so the run fell back to loose and the rules that need a "
            + "compilation did not run. Everything below covers the rest with the rules that could run; the "
            + "SKIPPED line names the rules that did not run.",
            text,
            StringComparison.Ordinal
        );
        Assert.Equal(Refused, Assert.Single(Renderer.BlockedFiles(report)));
        Assert.Equal([(IncompleteCause.Defect, 1)], Renderer.Causes(report));
    }

    /// <summary>
    ///     ⚠ Both non-file causes at once, so the two sentences and the trailer compose rather than
    ///     one silencing the other.
    /// </summary>
    [Fact]
    public void AgentBanner_NamesBothABaselineAndAProject_WhenBothAreOutsideTheFraction() {
        var report = Report(ConflictedBaseline(), ProjectThatWouldNotLoad()) with { FileCount = 1 };
        var text = Renderer.Render(report, ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  the baseline at .skala/baseline.sarif could not be read, so the gate compared against "
            + "nothing. Broken.csproj could not be loaded, so the run fell back to loose and the rules that need "
            + "a compilation did not run. Every file was checked by the rules that could run; everything below is "
            + "shown as if there were nothing to compare against; the SKIPPED line names the rules that did not "
            + "run.",
            text,
            StringComparison.Ordinal
        );
        Assert.Equal(2, Renderer.OutsideTheFraction(report).Count);
        Assert.Empty(Renderer.BlockedFiles(report));
    }

    /// <summary>
    ///     ⚠ The same ids at warning never reach the banner: MSBuild's relayed <c>workspace:</c> lines,
    ///     the no-project-found case, and the binlog rung's per-assembly <c>SK9029</c>. Each is a state
    ///     the repository is in, not a rung that failed, and <c>ReliabilityGateTests</c> pins the same
    ///     split at the gate.
    /// </summary>
    [Fact]
    public void AgentBanner_IsSilentForARelayedWorkspaceLine() {
        var text = Renderer.Render(Report(RelayedWorkspaceLine()) with { FileCount = 1 }, ReportFormat.Agent);

        Assert.StartsWith("OK  nothing to do.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("INCOMPLETE", text, StringComparison.Ordinal);
        Assert.Empty(Renderer.OutsideTheFraction(Report(RelayedWorkspaceLine())));
    }

    /// <summary>
    ///     ⚠ <b>The invariant that replaced <c>Scale</c>'s <c>FileCount &lt; blocked</c> guard</b>: every
    ///     path <c>BlockedFiles</c> yields is one the loader counted, so the fraction never needs a
    ///     branch to stop it printing <c>1 of 0</c>. It is asserted per tool id, over the whole
    ///     <c>SK9xxx</c> range as <c>rules.json</c> registers it, and every id has to be on exactly one
    ///     of three lists — which is the decision #356, #360 and #361 each had to make after the fact.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Located at a counted source file</b>: the loader that emitted it put the path into
    ///         <c>ReportablePaths</c> or <c>UnreadablePaths</c> (#356), or the stage that emitted it was
    ///         handed the path from <c>ReportablePaths</c>. These are the only ids allowed to be a
    ///         blocked file. <b>Not about a file</b>: <c>IsAboutAFile</c> is false, so the banner gives
    ///         them a sentence outside the arithmetic. <b>Never blocking in a report</b>: warning at
    ///         most, located at the root, refused at exit 4 before a renderer runs, or
    ///         <c>config check</c>'s — asserted by name so that promoting one to error-at-a-path is a
    ///         change to this list and not a silent new way in.
    ///     </para>
    ///     <para>
    ///         ⚠ Sabotage: move <c>SK9024</c> to the first list, or drop it from <c>CauseOf</c>, and the
    ///         classification half goes red; the arithmetic half then prints <c>1 of 0</c> for it, which
    ///         is the sentence the guard used to hide.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryBlockingToolId_IsEitherACountedSourceFileOrOutsideTheFraction() {
        string[] locatedAtACountedSourceFile = ["SK9010", "SK9015", "SK9095", "SK9096", "SK9097", "SK9098", "SK9099"];
        string[] notAboutAFile = ["SK9024", "SK9028", "SK9029"];
        string[] neverBlockingInAReport = [
            "SK9001", "SK9002", "SK9003", "SK9004", "SK9005", "SK9006", "SK9007", "SK9008", "SK9009", "SK9011",
            "SK9012", "SK9013", "SK9014", "SK9016", "SK9017", "SK9020", "SK9021", "SK9022", "SK9023", "SK9025",
            "SK9026", "SK9027", "SK9030", "SK9031"
        ];

        var registered = RuleCatalog.All
            .Select(static rule => rule.Id)
            .Where(static id => id.StartsWith("SK9", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var classified = locatedAtACountedSourceFile
            .Concat(notAboutAFile)
            .Concat(neverBlockingInAReport)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Anti-vacuity: a new tool id has to be placed on a list, and no id may be on two.
        Assert.Equal(registered, classified);

        foreach (var id in locatedAtACountedSourceFile) {
            var diagnostic = new SkalaDiagnostic(id, SkalaSeverity.Error, "m", Broken);
            Assert.True(Renderer.IsAboutAFile(Renderer.CauseOf(diagnostic)), id);
            Assert.Equal(Broken, Assert.Single(Renderer.BlockedFiles(Report(diagnostic))));
        }

        foreach (var id in notAboutAFile) {
            var diagnostic = new SkalaDiagnostic(id, SkalaSeverity.Error, "m", Project);
            Assert.False(Renderer.IsAboutAFile(Renderer.CauseOf(diagnostic)), id);

            // The arithmetic half: over a tree with nothing counted, the id is not a blocked file and
            // the fraction is never asked for.
            var report = Report(diagnostic) with { FileCount = 0 };
            Assert.Empty(Renderer.BlockedFiles(report));
            var text = Renderer.Render(report, ReportFormat.Agent);
            Assert.DoesNotContain("file was not checked", text, StringComparison.Ordinal);
            Assert.DoesNotContain(" of 0 ", text, StringComparison.Ordinal);
        }
    }
}
