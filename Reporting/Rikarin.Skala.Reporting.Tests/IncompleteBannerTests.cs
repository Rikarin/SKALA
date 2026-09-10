using Rikarin.Skala.Core.Diagnostics;

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
            $"A reproduction is in {Path.Combine(Root, ".skala", "crash", "1a2b3c")}. This is a Skala bug; the file was left untouched."
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
        Assert.DoesNotContain("Skala bug", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The banner says what it is instead, and says what to check.</summary>
    [Fact]
    public void AgentBanner_SaysUnreadableAndWhatToCheck() {
        var text = Renderer.Render(Report(Unreadable()), ReportFormat.Agent);

        Assert.StartsWith(
            "INCOMPLETE  1 of 4 file was not checked — could not be read: check permissions and that the path is still mounted.",
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
            + "2 could not be read (check permissions and that the path is still mounted). Everything below covers the rest.",
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
}
