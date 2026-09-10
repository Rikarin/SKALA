using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #356: a file the load could not read is in the denominator of the <c>INCOMPLETE</c> fraction
///     and in the <c>PARTIAL</c> trailer's arithmetic, under every loader.
/// </summary>
/// <remarks>
///     <para>
///         <c>RunReport.FileCount</c> was the size of <c>ReportablePaths</c>, and the loose loader
///         added a path there only after it had read the file. So over a two-file tree with one
///         mode-000 file <c>verify</c> printed <c>1 of 1 file was not checked</c> and, directly under
///         a finding on the readable neighbour, <c>0 files were checked</c>. With two unreadable files
///         beside one readable one the renderer noticed <c>FileCount &lt; blocked</c> and dropped the
///         fraction rather than print the impossible <c>2 of 1</c>. Measured through the real binary
///         before the fix; the CLI twin of this class re-measures it there.
///     </para>
///     <para>
///         ⚠ <b>The three loaders were audited before one was changed, and they were three different
///         defects.</b> <c>loose</c> reported the file and left it out of the count. <c>binlog</c>
///         caught <c>IOException</c> alone at the read, so the <c>UnauthorizedAccessException</c>
///         escaped the loader and the command printed <c>skala: Access to the path … is denied.</c>
///         at exit 5 with no report — the shape #353 had removed from <c>loose</c>. <c>workspace</c>
///         never opens a document itself; Roslyn does, and Roslyn's answer to a denied read is an
///         <em>empty</em> document, so <c>check --no-formatting</c> printed <c>OK  nothing to do.</c>
///         at exit 0 with the right file count and no diagnostic of any kind. One shared read
///         (<c>SourceFiles</c>) and one shared property (<c>CompilationUnit.UnreadablePaths</c>) now
///         give the three loaders the same three outcomes.
///     </para>
///     <para>
///         ⚠ Every fixture decides by attempting the read (<see cref="Scratch.WriteUnreadable" />). As
///         root a mode-000 file opens fine and every assertion here would hold vacuously.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class UnreadableFileCountTests {
    const string Skip = "needs a POSIX mode bit this process is subject to; root and Windows are exempt.";

    /// <summary>Carries an SK0001 so "the readable neighbour was reached" is visible in the report.</summary>
    const string Unformatted = """
                               namespace Scratch;

                               public sealed class Open
                               {
                                   public   int  Value {get;set;}
                               }
                               """;

    const string Locked = """
                          namespace Scratch;

                          public sealed class Locked {
                              public int Value { get; set; }
                          }
                          """;

    const string Project = """
                           <Project Sdk="Microsoft.NET.Sdk">
                             <PropertyGroup>
                               <TargetFramework>net10.0</TargetFramework>
                               <Nullable>enable</Nullable>
                             </PropertyGroup>
                           </Project>
                           """;

    static CheckRequest Request(Scratch scratch, LoadMode mode, params string[] paths) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = paths.Length == 0 ? [scratch.Root] : paths,
            Mode = mode,
            AllowLoadFallback = false,
            Output = string.Empty,
            IncludeMetrics = false,
            NoCache = true
        };

    static VerifyRequest Verify(Scratch scratch) =>
        new() { RepositoryRoot = scratch.Root, Paths = [scratch.Root], Mode = LoadMode.Loose, NoCache = true };

    /// <summary>
    ///     The loader-level shape: the unreadable file is in one set, the readable one in the other,
    ///     and exactly one <c>SK9015</c> names it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Sabotage:</b> move <c>unreadable.Add</c> back behind the read — or count the file into
    ///     <c>ReportablePaths</c> instead — and this fails on the set it lands in, before any renderer
    ///     is involved.
    /// </remarks>
    [Fact]
    public void Loose_PutsAnUnreadableFileInTheCountAndNotInTheStagesSet() {
        using var scratch = new Scratch();
        var open = scratch.Write("Open.cs", Unformatted);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is not { } locked) {
            Assert.Skip(Skip);
            return;
        }

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose, AllowFallback = false },
            TestContext.Current.CancellationToken
        );

        var unit = Assert.Single(loaded.Units);
        Assert.Equal(open, Assert.Single(unit.ReportablePaths));
        Assert.Equal(locked, Assert.Single(unit.UnreadablePaths));

        var diagnostic = Assert.Single(loaded.Diagnostics);
        Assert.Equal(FormatDiagnosticIds.FileIoFailed, diagnostic.Id);
        Assert.Equal(locked, diagnostic.File);
    }

    /// <summary>The issue's own case: two files, one unreadable — <c>1 of 2</c>, and <c>1 file was checked</c>.</summary>
    [Fact]
    public void Verify_TwoFilesOneUnreadable_SaysOneOfTwoAndOneChecked() {
        using var scratch = new Scratch();
        scratch.Write("Open.cs", Unformatted);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is null) {
            Assert.Skip(Skip);
            return;
        }

        var result = VerifyCommand.Run(Verify(scratch), TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Contains("1 of 2 file was not checked", result.Output, StringComparison.Ordinal);
        Assert.Contains("PARTIAL  1 file was checked", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("0 files were checked", result.Output, StringComparison.Ordinal);

        // The readable neighbour was reached: its finding is the thing the old trailer contradicted.
        Assert.Contains("Open.cs", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The case the renderer used to hide. Two unreadable files beside one readable one made
    ///     <c>FileCount</c> 1 and <c>blocked</c> 2, and <c>Scale</c> dropped the fraction rather than
    ///     print <c>2 of 1</c>. The fraction is now present and right.
    /// </summary>
    [Fact]
    public void Verify_ThreeFilesTwoUnreadable_KeepsTheFraction() {
        using var scratch = new Scratch();
        scratch.Write("Open.cs", Unformatted);
        if (scratch.WriteUnreadable("LockedA.cs", Locked) is null
            || scratch.WriteUnreadable("LockedB.cs", Locked.Replace("Locked", "LockedB", StringComparison.Ordinal))
            is null) {
            Assert.Skip(Skip);
            return;
        }

        var result = VerifyCommand.Run(Verify(scratch), TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Contains("2 of 3 files were not checked", result.Output, StringComparison.Ordinal);
        Assert.Contains("PARTIAL  1 file was checked", result.Output, StringComparison.Ordinal);
    }

    /// <summary>The control: a tree with nothing unreadable counts exactly what it counted before.</summary>
    [Fact]
    public void ReadableFilesOnly_CountUnchanged() {
        using var scratch = new Scratch();
        scratch.Write("Open.cs", Unformatted);
        scratch.Write("Other.cs", Locked);

        var (result, report) = CheckCommand.Run(Request(scratch, LoadMode.Loose), TestContext.Current.CancellationToken);

        Assert.NotEqual(ExitCodes.InternalError, result.ExitCode);
        Assert.Equal(2, report.FileCount);
        Assert.DoesNotContain(report.Diagnostics, static d => d.Id == FormatDiagnosticIds.FileIoFailed);
        Assert.All(report.Findings, static finding => Assert.NotEqual(FormatDiagnosticIds.FileIoFailed, finding.RuleId));
    }

    /// <summary>
    ///     ⚠ The one-file shape of the same defect. <c>check Locked.cs</c> found the file, could not
    ///     read it, and — because the reported set was empty — answered #346's "nothing under
    ///     'Locked.cs' is part of this load" at exit 3, as though the path were a typo. It is
    ///     <c>1 of 1</c>, <c>SK9015</c>, exit 5.
    /// </summary>
    [Fact]
    public void RequestedFileThatCannotBeRead_IsReportedRatherThanRefused() {
        using var scratch = new Scratch();
        scratch.Write("Open.cs", Unformatted);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is not { } locked) {
            Assert.Skip(Skip);
            return;
        }

        var (result, report) = CheckCommand.Run(
            Request(scratch, LoadMode.Loose, locked),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Equal(1, report.FileCount);
        Assert.Contains(report.Diagnostics, d => d.Id == FormatDiagnosticIds.FileIoFailed && d.File == locked);
        Assert.Empty(report.Findings);
    }

    /// <summary>
    ///     ⚠ <c>workspace</c>: the loader that was silently wrong rather than loudly wrong. Roslyn
    ///     substitutes an empty document for a file it cannot read, and with the formatting stage off
    ///     nothing in the run ever opened the file itself — <c>OK  nothing to do.</c>, exit 0,
    ///     <c>fileCount: 2</c>, measured. The load now attempts the read and says so.
    /// </summary>
    /// <remarks>
    ///     <c>IncludeFormatting = false</c> is the point: with it on, <c>FormattingFindings</c> would
    ///     open the file and emit its own <c>SK9015</c>, and the assertion could pass with the loader
    ///     still silent. Sabotage by reverting <c>WorkspaceLoader</c>'s read attempt: exit 0.
    /// </remarks>
    [Fact]
    public void Workspace_ReportsAndCountsAnUnreadableDocumentTheAnalyzersNeverSaw() {
        using var scratch = new Scratch();
        var project = scratch.Write("Probe.csproj", Project);
        scratch.Write("Directory.Build.props", "<Project />");
        scratch.Write("Open.cs", Unformatted);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is not { } locked) {
            Assert.Skip(Skip);
            return;
        }

        var (result, report) = CheckCommand.Run(
            Request(scratch, LoadMode.Workspace) with { ProjectPath = project, IncludeFormatting = false },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(LoadMode.Workspace, report.Mode);
        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Equal(2, report.FileCount);
        Assert.Single(report.Diagnostics, d => d.Id == FormatDiagnosticIds.FileIoFailed && d.File == locked);
    }

    /// <summary>
    ///     ⚠ <c>binlog</c>: the loader that still crashed. Its read caught <c>IOException</c> alone,
    ///     so a mode-000 source named by the binlog took the whole command down — no report, no
    ///     count, <c>skala: Access to the path … is denied.</c> on stderr. The same read as the other
    ///     two now; and the staleness check, which reports a source it finds in no compilation as
    ///     "rebuild", knows the file was in one.
    /// </summary>
    /// <remarks>
    ///     Built rather than faked: a binlog is the record of a real build, so this shells out to
    ///     <c>dotnet build -bl</c> with both files readable and locks one afterwards — which is also
    ///     the real sequence, since a file nobody can read does not compile.
    /// </remarks>
    [Fact]
    public void Binlog_ReportsAndCountsAnUnreadableSourceInsteadOfCrashing() {
        using var scratch = new Scratch();
        var project = scratch.Write("Probe.csproj", Project);
        scratch.Write("Directory.Build.props", "<Project />");
        scratch.Write("Open.cs", Unformatted);
        var lockedPath = scratch.Write("Locked.cs", Locked);
        var binlog = Path.Combine(scratch.Root, "probe.binlog");
        Build(project, binlog);

        File.Delete(lockedPath);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is not { } locked) {
            Assert.Skip(Skip);
            return;
        }

        var (result, report) = CheckCommand.Run(
            Request(scratch, LoadMode.Binlog) with { BinlogPath = binlog, IncludeFormatting = false },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(LoadMode.Binlog, report.Mode);
        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Equal(2, report.FileCount);
        Assert.Single(report.Diagnostics, d => d.Id == FormatDiagnosticIds.FileIoFailed && d.File == locked);
        Assert.DoesNotContain(report.Diagnostics, d => d.Id == RuleIds.BinlogMissingFile && d.File == locked);
    }

    static void Build(string project, string binlog) {
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };

        foreach (var argument in new[] { "build", project, "-bl:" + binlog, "--nologo" }) {
            start.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, "`dotnet build` on the binlog fixture failed:\n" + output);
    }
}
