using Rikarin.Skala.Testing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     The exit codes docs/plan/09 § "Exit codes" publishes, asserted against the real binary.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>The contract was wrong from M1 to M9 and every test in the tree agreed with it.</b>
///         <c>ReportingTests.ExitCodes_AreTheOnesHooksAndCiDependOn</c> asserted
///         <c>ExitCodes.FormattingNeeded == 2</c> — true, and useless, because <c>format</c> did not use
///         <c>ExitCodes</c>. It used <c>FormatCommand.ChangesFound</c>, which was 1, and
///         <c>FormatCommand.Failed</c>, which was 2: the published table inverted. A hook told to
///         auto-format on 2 and stop on 1 did the opposite of both.
///     </para>
///     <para>
///         So this class asserts <em>behaviour</em>, through the process boundary, for each row of the
///         table that a command can actually produce. A constant compared against another constant cannot
///         fail when both are wrong together; a command that exits 2 can only be made to do so by exiting
///         2. The one thing it does not do is trust the document from memory —
///         <see cref="TheDocumentStillSaysWhatThisClassAsserts" /> reads the table out of
///         <c>docs/plan/09</c>, so a future edit that changes the document has to change these tests too,
///         which is the conversation that should happen.
///     </para>
/// </remarks>
public sealed class ExitCodeContractTests : IDisposable {
    /// <summary>⚠ One constant: <c>SK7083</c>'s threshold is five literals per file.</summary>
    const string LoadOption = "--load";

    const string Loose = "loose";

    const string FormatOption = "--format";

    const string Agent = "agent";

    readonly string directory = Directory.CreateTempSubdirectory("skala-exit-").FullName;

    public void Dispose() => Directory.Delete(directory, true);

    string Write(string name, string content) {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Zero_WhenThereIsNothingToDo() {
        var path = Write("Clean.cs", "class C {\n    void M() {\n        M();\n    }\n}\n");
        Assert.Equal(0, CliRunner.Run("format", "--check", path).ExitCode);
    }

    /// <summary>⚠ The row the tool got backwards. 2, never 1.</summary>
    [Fact]
    public void Two_WhenFormattingIsNeeded() {
        var path = Write("Dirty.cs", "class  C{ void  M( ){} }\n");
        var run = CliRunner.Run("format", "--check", path);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("1 file would be reformatted", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <c>--diff</c> reports exactly what <c>--check</c> reports, so it exits the same way.
    /// </summary>
    [Fact]
    public void Two_WhenDiffFindsEdits() {
        var path = Write("Diffed.cs", "class  C{ void  M( ){} }\n");
        Assert.Equal(2, CliRunner.Run("format", "--diff", path).ExitCode);
    }

    /// <summary>
    ///     ⚠ <c>arrange --check</c> is a formatting check and shares the row, which was the second
    ///     copy of the same inverted pair.
    /// </summary>
    [Fact]
    public void Two_WhenArrangeFindsChanges() {
        // `using` after a type is the one arrangement finding that needs no compilation.
        var path = Write("Arranged.cs", "class C {\n}\n\nusing System;\n");
        var run = CliRunner.Run("arrange", "--check", path);

        Assert.True(
            run.ExitCode is 0 or 2,
            $"arrange --check exited {run.ExitCode}; the only codes it may produce here are 0 and 2. "
            + run.StandardOutput
        );
    }

    /// <summary>An unrecognized option is a configuration error, not a failed gate.</summary>
    [Fact]
    public void Three_WhenAnOptionIsNotRecognized() {
        var run = CliRunner.Run("check", LoadOption, Loose, "--verbsoe");

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("--verbsoe", run.StandardOutput + run.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ And it names the path rather than analysing an empty set.
    /// </summary>
    /// <remarks>
    ///     <c>format --check no-such-dir</c> exited <b>0</b> — "0 files would be reformatted, 0 left
    ///     alone" — because a directory that is not there contributes no files, and no files is
    ///     indistinguishable from no findings. A gate that passes on a typo is quiet in exactly the
    ///     case it exists for.
    /// </remarks>
    [Fact]
    public void Three_WhenAPathDoesNotExist() {
        var run = CliRunner.Run("format", "--check", "no-such-directory-anywhere");

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("does not exist", run.StandardOutput + run.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A refusal to run is 3, not 2. A hook that auto-formats on 2 would read this refusal as an
    ///     instruction to do the thing it just refused.
    /// </summary>
    [Fact]
    public void Three_WhenAnInvocationIsRefused() {
        var run = CliRunner.Run("config", "diff", CliRunner.Template);

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("needs two files", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The flag that was not a flag: <c>--verbose</c> bound to the variadic <c>&lt;paths&gt;</c>,
    ///     so <c>check --verbose</c> looked for C# files in a directory called "--verbose", found none,
    ///     and exited 4 from a repository full of them.
    /// </summary>
    [Fact]
    public void Verbose_IsAnOptionAndNotAPath() {
        var path = Write("Verbose.cs", "class C {\n    void M() {\n        M();\n    }\n}\n");
        var run = CliRunner.Run("format", "--check", "--verbose", path);

        Assert.Equal(0, run.ExitCode);
        Assert.DoesNotContain("SK9023", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("0 files would be reformatted", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ And it is recursive, so it means the same thing on every verb. A flag a script puts in a
    ///     variable has to be accepted wherever the variable is used.
    /// </summary>
    [Theory]
    [InlineData("format")]
    [InlineData("arrange")]
    [InlineData("check")]
    [InlineData("verify")]
    [InlineData("fix")]
    public void Verbose_IsAcceptedByEveryVerbThatTakesPaths(string verb) {
        var run = CliRunner.Run(verb, "--verbose", "--help");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("--verbose", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A path that genuinely begins with <c>-</c> is still reachable, spelled the way every other
    ///     POSIX tool requires. The guard rejects mistyped options, not filenames.
    /// </summary>
    [Fact]
    public void ADashedFilename_IsStillReachable() {
        var path = Write("-dashed.cs", "class  C{ }\n");
        Assert.Equal(2, CliRunner.Run("format", "--check", path).ExitCode);
    }

    /// <summary>
    ///     A <c>.csproj</c> naming an SDK that does not exist: MSBuild records a failure and hands back no
    ///     documents.
    /// </summary>
    const string UnloadableProject = """
                                     <Project Sdk="Definitely.Not.A.Real.Sdk">
                                       <PropertyGroup>
                                         <TargetFramework>net10.0</TargetFramework>
                                       </PropertyGroup>
                                     </Project>
                                     """;

    /// <summary>
    ///     ⚠ #361: a project the default ladder found and could not load is exit 1, not 0, and the
    ///     banner above it names the project rather than a file.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Measured on <c>master</c> with this binary and this tree:
    ///         <c>
    /// INCOMPLETE  1 of 1 file was
    ///         not checked — this is a Skala bug, not a finding in your code.
    ///         </c> above <b>exit 0</b>,
    ///         then <c>SKIPPED 260 rule(s) did not run (loose load)</c>. The "1 file" was
    ///         <c>Broken.csproj</c>. The source file was checked by the loose rung and its finding
    ///         rendered under the banner; 260 rules did not run; the <c>local</c> gate passed.
    ///     </para>
    ///     <para>
    ///         The scratch gets a <c>.git</c> so it is its own repository root: the banner's relative
    ///         path is then <c>Broken.csproj</c> and the run's <c>.skala/</c> lands here, not in this
    ///         checkout. Sabotage: remove the <c>SK9024</c>/<c>SK9029</c> clause from
    ///         <c>Gate.EvaluateReliability</c> and the exit goes back to 0.
    ///     </para>
    /// </remarks>
    [Fact]
    public void One_WhenAProjectIsFoundAndWillNotLoad_UnderTheDefaultLadder() {
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        Write("One.cs", "public class D {\n    public int Value;\n}\n");
        Write("Broken.csproj", UnloadableProject);

        var run = CliRunner.Run("check", LoadOption, "binlog", "--gate", "local", FormatOption, Agent, directory);
        var text = run.StandardOutput + run.StandardError;

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(
            "INCOMPLETE  Broken.csproj could not be loaded, so the run fell back to loose",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("SK9024  Broken.csproj", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Skala bug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 of 1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("was not checked", text, StringComparison.Ordinal);

        // The syntactic half was delivered and is shown.
        Assert.Contains("SK6030  One.cs:1", text, StringComparison.Ordinal);
        Assert.Contains("SKIPPED", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The behaviour that must not move: name the mode and the same project is refused at exit 4
    ///     before any renderer runs — no banner, no findings, the load diagnostics and nothing else.
    /// </summary>
    [Fact]
    public void Four_WhenTheNamedModeCannotLoadTheProject() {
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        Write("One.cs", "public class D {\n    public int Value;\n}\n");
        Write("Broken.csproj", UnloadableProject);

        var run = CliRunner.Run("check", LoadOption, "workspace", "--gate", "local", FormatOption, Agent, directory);
        var text = run.StandardOutput + run.StandardError;

        Assert.Equal(4, run.ExitCode);
        Assert.Contains("no compilation could be built", text, StringComparison.Ordinal);
        Assert.Contains("SK9024", text, StringComparison.Ordinal);
        Assert.DoesNotContain("INCOMPLETE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SK6030", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ 5 is "internal error", and the row had no behavioural test until SK-FUZZ-0001.
    /// </summary>
    /// <remarks>
    ///     ⚠ The defect that wanted this: an <c>IndexOutOfRangeException</c> out of <c>EditEmitter</c>
    ///     escaped every per-command handler, System.CommandLine returned the action's default, and a
    ///     crash on a 32-byte file reported <b>0</b> from this binary and <b>1</b> from the coordinator's.
    ///     Both are a wrong <em>success-shaped</em> answer — 0 says "clean", 1 says "your code failed the
    ///     gate" — and in CI a crash was then indistinguishable from a finding. It is the same class as
    ///     M7's daemon exiting 0 while dying.
    ///     <para>
    ///         The input below is the reachable half of the row: <c>SK9099</c>, the formatter's safety net
    ///         tripping on a file it cannot format. The unreachable half is now a top-level handler in
    ///         <c>Program.cs</c> mapping any unhandled exception to 5, and it is verified the only way a
    ///         handler for the impossible can be: by making it happen on purpose and watching it.
    ///     </para>
    ///     <para>
    ///         ⚠ The trigger used to be a live open defect — SK-FUZZ-0002, a <c>///</c> run beginning on the
    ///         brace line — with a note here saying that fixing it should give this test a different trigger
    ///         rather than delete it. It was fixed, and this is that trigger.
    ///     </para>
    ///     <para>
    ///         ⚠ It is forced, because <b>no input trips SK9099 any more</b> and that is the good news it
    ///         looks like: all three that ever did are fixed and retired (SK-FUZZ-0001, -0005, -0002), and a
    ///         scan of all 1 520 files of <c>corpus/unformatted/</c> — the most deliberately mangled input
    ///         the project has — produces not one. <c>SKALA_FORCE_SK9099</c> makes the safety net refuse the
    ///         file it names, inside the formatter, so everything downstream of the refusal is still real:
    ///         the diagnostic's text, <c>FormatCommand</c>'s failure counting, and the code the process
    ///         returns. Faking the exit code instead would test nothing.
    ///     </para>
    ///     <para>
    ///         ⚠ If a real SK9099 case is ever found again it belongs here in place of the seam — and
    ///         as a GitHub issue first.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Five_WhenTheSafetyNetRefusesAFile() {
        var path = Write("Refused.cs", "class C {\n    void M() { }\n}\n");
        var run = CliRunner.RunWith(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["SKALA_FORCE_SK9099"] = "Refused.cs" },
            "format",
            path
        );

        Assert.Equal(5, run.ExitCode);
        Assert.Contains("SK9099", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #362: a crashed analyzer is exit 1 under a banner that names it — never under
    ///     <c>OK  nothing to do.</c>, and never under zero bytes.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Measured on <c>master</c> with this binary and this tree, before the fix:
    ///         <c>check --format agent</c> printed <c>OK  nothing to do.</c> above <b>exit 1</b>, and
    ///         <c>--format plain</c> — the default whenever stdout is not a terminal, which is every CI
    ///         log — printed <b>nothing at all</b> above the same exit 1. The gate had failed on
    ///         <c>SK9030</c> at warning; the banner keyed on error.
    ///     </para>
    ///     <para>
    ///         ⚠ Forced, the way <see cref="Five_WhenTheSafetyNetRefusesAFile" /> forces <c>SK9099</c>:
    ///         no Skala analyzer throws on any input the corpus holds, and hosting a throwing
    ///         third-party one means writing a package under the user's <c>~/.skala/packages</c>.
    ///         <c>SKALA_FORCE_SK9030</c> adds a real analyzer that really throws, so everything downstream
    ///         is real — Roslyn's exception callback, the diagnostic's text, the gate clause, the banner
    ///         and the exit. Sabotage: key <c>Renderer.Blocking</c> back on severity alone and both
    ///         formats go red on the sentence while the exit stays 1.
    ///     </para>
    ///     <para>
    ///         The scratch gets a <c>.git</c> so it is its own repository root and the run's
    ///         <c>.skala/</c> lands here, not in this checkout.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("check", Agent)]
    [InlineData("check", "plain")]
    [InlineData("verify", Agent)]
    [InlineData("verify", "plain")]
    public void One_WhenAnAnalyzerThrows_AndTheOutputSaysSo(string verb, string format) {
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        Write("Clean.cs", "namespace Demo;\n\npublic sealed class Clean {\n    public int Value { get; init; }\n}\n");

        var run = CliRunner.RunWith(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["SKALA_FORCE_SK9030"] = "1" },
            verb,
            LoadOption,
            Loose,
            FormatOption,
            format,
            directory
        );
        var text = run.StandardOutput + run.StandardError;

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("SK9030", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("ForcedCrash", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("threw once", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("OK", text, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing to do", text, StringComparison.Ordinal);

        // ⚠ A crash is not a cancellation: the unit finished, and `SK9027 'loose' was cancelled` used
        // to be printed beside the `SK9030`, which was the tool contradicting itself about one run.
        Assert.DoesNotContain("SK9027", text, StringComparison.Ordinal);
        Assert.DoesNotContain("cancelled", text, StringComparison.Ordinal);

        if (format == Agent) {
            Assert.StartsWith(
                "INCOMPLETE  an analyzer threw (SK9030 below)",
                run.StandardOutput,
                StringComparison.Ordinal
            );
        }
    }

    /// <summary>
    ///     #363: the second run over the same cache fails the same way, instead of serving the crashed
    ///     run's silence as a clean tree.
    /// </summary>
    /// <remarks>
    ///     Measured before the fix, on this binary and this fixture: run one exit 1 with <c>SK9030</c>,
    ///     run two exit 0 with no <c>SK9030</c> and the <c>SK2014</c> served from the cache. The
    ///     crashed analyzer's absence had been written to <c>.skala/cache/</c> as "no findings".
    ///     <para>
    ///         ⚠ Loose and the same environment both times, on purpose. Loose is the one load mode
    ///         whose warm path is reachable, and the forced analyzer is part of the rule-set
    ///         fingerprint, so a second run <em>without</em> the variable would miss every key and
    ///         measure nothing. The in-process half of this — that the warm path was taken, by
    ///         <c>CacheHits</c> — is <c>CrashedRunCacheTests</c> in Analysis; this row is the process
    ///         boundary.
    ///     </para>
    /// </remarks>
    [Fact]
    public void One_WhenAnAnalyzerThrows_AndAgainOnTheNextRunOverTheSameCache() {
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        Write(
            "Swallow.cs",
            "namespace Demo;\n\npublic static class Swallow {\n    public static void Run() {\n"
            + "        try {\n            System.Console.WriteLine();\n        } catch {\n        }\n    }\n}\n"
        );
        var forced = new Dictionary<string, string>(StringComparer.Ordinal) { ["SKALA_FORCE_SK9030"] = "1" };

        var first = CliRunner.RunWith(forced, "check", LoadOption, Loose, FormatOption, "plain", directory);
        Assert.Equal(1, first.ExitCode);
        Assert.Contains("SK9030", first.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SK2014", first.StandardOutput, StringComparison.Ordinal);

        var second = CliRunner.RunWith(forced, "check", LoadOption, Loose, FormatOption, "plain", directory);
        Assert.Equal(1, second.ExitCode);
        Assert.Contains("SK9030", second.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SK2014", second.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The positive control for the row above: the same tree without the switch is
    ///     <c>OK  nothing to do.</c> at exit 0, so the fix is not "never print the sentence".
    /// </summary>
    [Fact]
    public void Zero_WhenNoAnalyzerThrows_IsStillNothingToDo() {
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        Write("Clean.cs", "namespace Demo;\n\npublic sealed class Clean {\n    public int Value { get; init; }\n}\n");

        var run = CliRunner.Run("check", LoadOption, Loose, FormatOption, Agent, directory);

        Assert.Equal(0, run.ExitCode);
        Assert.StartsWith("OK  nothing to do.", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A file the process is not permitted to read, or null when this machine cannot produce one.
    /// </summary>
    /// <remarks>
    ///     ⚠ The guard is a <em>read attempt</em>, not a platform check, and that is deliberate.
    ///     Windows is skipped because its equivalent is an ACL rather than a mode bit, but the case
    ///     that actually makes this test lie is <b>running as root</b>, which reads a mode-000 file
    ///     happily — in a CI container, that is the default. A test that cannot fail is the defect, so
    ///     rather than guessing at a uid this asks the only question that matters: after the chmod,
    ///     can this process still open the file? If it can, there is nothing here to assert.
    /// </remarks>
    string? UnreadableFile(string name) {
        if (OperatingSystem.IsWindows()) {
            return null;
        }

        var path = Write(name, "class C {\n    void M() {\n        M();\n    }\n}\n");
        File.SetUnixFileMode(path, UnixFileMode.None);

        try {
            File.ReadAllText(path);
            // Root, or a filesystem that does not enforce the bits. Undo it so teardown can delete.
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return null;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return path;
        }
    }

    /// <summary>
    ///     ⚠ #353. An unreadable file is <b>5</b>, and it is not a crash.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>UnauthorizedAccessException</c> does not derive from <c>IOException</c> — it derives
    ///         from <c>SystemException</c> — so the <c>catch (IOException)</c> in each per-file loop
    ///         did not hold it. Measured before the fix, all three verbs got this wrong and they got
    ///         it wrong in three different ways:
    ///     </para>
    ///     <para>
    ///         ⚠ <c>arrange --check</c> exited <b>2</b>. 2 is "formatting changes are needed", so a
    ///         pre-commit hook told to auto-format on 2 was being told to run the formatter over a
    ///         file nobody could read. <c>format --check</c> exited <b>5</b> with a full stack trace
    ///         under "skala: internal error — this is a Skala bug", because <c>FormatAll</c>'s
    ///         <c>Parallel.For</c> wraps an escape in an <c>AggregateException</c> that matches
    ///         neither top-level handler — and it exited <b>2</b> instead when handed a single file,
    ///         because that path runs serially. Same file, same permissions, two answers depending on
    ///         how many files were beside it. <c>verify</c> aborted with no verdict at all.
    ///     </para>
    ///     <para>
    ///         ⚠ Sabotage check: narrow either catch back to <c>catch (IOException …)</c> and this
    ///         goes red — verified by doing it, on both the format and the arrange site.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>5 and not 4 is decided (#357), not inherited.</b> #355 and #356 each deferred the
    ///         question; the survey found no consumer to decide it for. Nothing outside the process
    ///         splits 4 from 5 — the MSBuild target, the MCP verdict, the pre-commit hook and CI all
    ///         read 0, the finding code and "other" — and everything inside it that does split them
    ///         reads 4 as "there is no report": <c>VerifyCommand.Verdict</c> appends the <c>PARTIAL</c>
    ///         trailer only on 5, and <c>fix</c> stops on 4. An unreadable file beside 753 checked ones
    ///         has a report, so 4 would either lose that trailer for the run it was written for or
    ///         stop meaning what its readers rely on. And the premise was wrong: 5 has read "an I/O
    ///         failure that stopped a file being read" since before #353; "this is a Skala bug" is the
    ///         unhandled-exception handler's sentence, not the exit code's. docs/plan/10 § "The
    ///         INCOMPLETE banner" holds the survey; <see cref="AnUnreadableFile_ExitsTheSameCodeFromEveryVerb" />
    ///         holds the four verbs together.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("format")]
    [InlineData("arrange")]
    public void Five_WhenAFileCannotBeRead(string verb) {
        if (UnreadableFile("Unreadable.cs") is not { } path) {
            Assert.Skip("needs a POSIX mode bit this process is subject to; root and Windows are exempt.");
            return;
        }

        var run = CliRunner.Run(verb, "--check", path);
        var text = run.StandardOutput + run.StandardError;

        Assert.Equal(5, run.ExitCode);

        // ⚠ It names the file, and it is not reported as a Skala bug. Both halves matter: the whole
        // point of SK9015 over SK9098 is that a mode-600 file owned by someone else is not a defect
        // in this tool, and a reader who is told it is will go looking in the wrong place.
        Assert.Contains("Unreadable.cs", text, StringComparison.Ordinal);
        Assert.Contains("SK9015", text, StringComparison.Ordinal);
        Assert.DoesNotContain("this is a Skala bug", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The count-dependent half, and the reason one file was not enough to catch this.
    /// </summary>
    /// <remarks>
    ///     <c>FormatAll</c> runs serially at <c>files.Count &lt;= 1</c> and through
    ///     <c>Parallel.For</c> above it, and only the parallel path produced the
    ///     <c>AggregateException</c> that defeated every handler. A fixture with a single file would
    ///     have exercised the branch that was merely wrong instead of the one that crashed — so this
    ///     one insists on a readable neighbour, and then insists that the neighbour was still
    ///     inspected. <b>Report it, leave it alone, keep going</b> is the contract; stopping at the
    ///     first unreadable file would satisfy the exit code and still lose the rest of the tree.
    /// </remarks>
    [Fact]
    public void AnUnreadableFile_DoesNotStopTheOnesBesideIt() {
        if (UnreadableFile("Unreadable.cs") is not { } path) {
            Assert.Skip("needs a POSIX mode bit this process is subject to; root and Windows are exempt.");
            return;
        }

        // Needs an edit of its own, so that "it was inspected" is visible in the output.
        Write("Neighbour.cs", "class  C{ void  M( ){} }\n");

        var run = CliRunner.Run("format", "--check", directory);
        var text = run.StandardOutput + run.StandardError;

        Assert.Equal(5, run.ExitCode);
        Assert.DoesNotContain("this is a Skala bug", text, StringComparison.Ordinal);
        Assert.Contains("SK9015", text, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(path), text, StringComparison.Ordinal);

        // The neighbour was reached, which is the half an exit code cannot show.
        Assert.Contains("1 file would be reformatted", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #357. The property #353 bought and nothing asserted in one place: the four verbs answer
    ///     the same unreadable file with the <b>same</b> exit code.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         #353's headline finding was three codes for one file — <c>arrange</c> 2, <c>format</c>
    ///         5 or 2 depending on how many files sat beside it, <c>verify</c> none at all. The tests
    ///         above pin each verb's number separately, which is what let the numbers drift apart in
    ///         the first place: a change to one verb goes red in one test and reads as that test's
    ///         problem. This one runs all four over one fixture and fails naming the verb that left
    ///         the others, so the next reader who finds a truer number for one of them meets the
    ///         other three before the change lands.
    ///     </para>
    ///     <para>
    ///         Two assertions, deliberately. "All equal" alone would hold if every verb moved to 0
    ///         together; "equal to 5" alone is the four tests above. The fixture carries a readable
    ///         neighbour so that <c>check</c> and <c>verify</c> have a compilation to build and the
    ///         run is the partial one the number describes, not an empty tree. <c>--load loose</c> on
    ///         both, because the fixture has no project and auto-load would otherwise go looking for
    ///         one above the temp directory.
    ///     </para>
    ///     <para>
    ///         ⚠ Sabotage: route <c>ArrangeCommand</c>'s I/O outcome to <c>ExitCodes.LoadFailure</c>
    ///         and this goes red on <c>arrange --check</c> with the other three still at 5 — verified
    ///         by doing it.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AnUnreadableFile_ExitsTheSameCodeFromEveryVerb() {
        if (UnreadableFile("Unreadable.cs") is null) {
            Assert.Skip("needs a POSIX mode bit this process is subject to; root and Windows are exempt.");
            return;
        }

        Write("Neighbour.cs", "class C {\n    void M() {\n        M();\n    }\n}\n");

        var codes = new Dictionary<string, int>(StringComparer.Ordinal) {
            ["arrange --check"] = CliRunner.Run("arrange", "--check", directory).ExitCode,
            ["format --check"] = CliRunner.Run("format", "--check", directory).ExitCode,
            ["check --load loose"] = CliRunner.Run("check", LoadOption, Loose, directory).ExitCode,
            ["verify --load loose"] = CliRunner.Run("verify", LoadOption, Loose, directory).ExitCode
        };

        var table = string.Join(", ", codes.Select(static entry => $"{entry.Key} -> {entry.Value}"));

        Assert.True(
            codes.Values.Distinct().Count() == 1,
            "the four verbs disagree about one unreadable file: " + table
        );
        Assert.True(codes.Values.All(static code => code == 5), "the shared code is not 5: " + table);
    }

    /// <summary>
    ///     ⚠ #355. The two tests above hold the per-file line to "not a Skala bug"; <c>verify</c>
    ///     printed "this is a Skala bug, not a finding in your code" one line above it, in the
    ///     INCOMPLETE banner, and "Exit 5 is that Skala bug" one line below it, in the PARTIAL
    ///     trailer — and neither test could see either, because neither ran <c>verify</c>. This one
    ///     does, against the real binary, and holds the <b>whole</b> output to the sentence.
    /// </summary>
    /// <remarks>
    ///     The exit code is asserted unchanged on purpose. #355 left it at 5 because #353 had just
    ///     pinned it there; #357 then decided it stays — the reasoning is on
    ///     <see cref="Five_WhenAFileCannotBeRead" />. Only the words moved here.
    /// </remarks>
    [Theory]
    [InlineData(Agent)]
    [InlineData("plain")]
    public void Verify_NeverCallsAnUnreadableFileASkalaBug(string format) {
        if (UnreadableFile("Unreadable.cs") is not { } path) {
            Assert.Skip("needs a POSIX mode bit this process is subject to; root and Windows are exempt.");
            return;
        }

        Write("Neighbour.cs", "class C {\n    void M() {\n        M();\n    }\n}\n");

        var run = CliRunner.Run("verify", FormatOption, format, directory);
        var text = run.StandardOutput + run.StandardError;

        Assert.Equal(5, run.ExitCode);
        Assert.Contains(Path.GetFileName(path), text, StringComparison.Ordinal);
        Assert.Contains("SK9015", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Skala bug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OK  nothing to do", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The control for the theory above, through the same binary: when the cause <em>is</em>
    ///     Skala — the formatter's safety net, forced the same way <see cref="Five_WhenTheSafetyNetRefusesAFile" />
    ///     forces it — <c>verify</c> still says so, in the banner and in the trailer. A fix that
    ///     deleted the sentence everywhere would pass the theory and fail here.
    /// </summary>
    [Fact]
    public void Verify_StillCallsTheSafetyNetASkalaBug() {
        var path = Write("Refused.cs", "class C {\n    void M() {\n        M();\n    }\n}\n");
        var run = CliRunner.RunWith(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["SKALA_FORCE_SK9099"] = "Refused.cs" },
            "verify",
            FormatOption,
            Agent,
            path
        );
        var text = run.StandardOutput + run.StandardError;

        Assert.Equal(5, run.ExitCode);
        Assert.Contains("SK9099", text, StringComparison.Ordinal);
        Assert.Contains("this is a Skala bug, not a finding in your code.", text, StringComparison.Ordinal);
        Assert.Contains("Exit 5 is that Skala bug, not a gate failure.", text, StringComparison.Ordinal);

        // ⚠ Once. The format and arrange stages both refuse the file and used to print the identical
        // line twice (#362, noted in passing); `Renderer.Blocking` is distinct by value.
        Assert.Single(Regex.Matches(text, @"SK9099  \S*Refused\.cs"));
    }

    /// <summary>
    ///     ⚠ #356. The banner's fraction and the trailer's arithmetic count a file the load could not
    ///     read, measured through the binary the issue was measured through.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The two-file tree the issue reports printed <c>1 of 1 file was not checked</c> and
    ///         <c>0 files were checked</c> under a finding on the readable neighbour: the loose loader
    ///         counted a file only once it had read it. The three-file tree with two unreadable files
    ///         is the case the renderer used to hide — <c>FileCount</c> 1 against 2 blocked files made
    ///         <c>Scale</c> drop the fraction rather than print <c>2 of 1</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ Sabotage: put <c>unreadable.Add</c> back behind the read in <c>LooseLoader</c> and
    ///         both rows go red on the fraction — verified by doing it.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData(1, "1 of 2 file was not checked", "PARTIAL  1 file was checked")]
    [InlineData(2, "2 of 3 files were not checked", "PARTIAL  1 file was checked")]
    public void Verify_CountsAnUnreadableFileInTheDenominator(int unreadable, string banner, string trailer) {
        for (var i = 0; i < unreadable; i++) {
            if (UnreadableFile("Unreadable" + i.ToString(CultureInfo.InvariantCulture) + ".cs") is null) {
                Assert.Skip("needs a POSIX mode bit this process is subject to; root and Windows are exempt.");
                return;
            }
        }

        Write("Neighbour.cs", "class  C{ void  M( ){} }\n");

        var run = CliRunner.Run("verify", FormatOption, Agent, LoadOption, Loose, directory);
        var text = run.StandardOutput + run.StandardError;

        Assert.Equal(5, run.ExitCode);
        Assert.Contains(banner, text, StringComparison.Ordinal);
        Assert.Contains(trailer, text, StringComparison.Ordinal);
        Assert.DoesNotContain("0 files were checked", text, StringComparison.Ordinal);

        // The neighbour was reached, which is what the trailer's count now agrees with.
        Assert.Contains("Neighbour.cs", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The table in the document, read rather than remembered.
    /// </summary>
    /// <remarks>
    ///     The rows this class exercises are pinned here against docs/plan/09's own table, so that
    ///     changing the document without changing the tool fails the build. This is the half that was
    ///     missing: the code and the document disagreed for four milestones and neither side was
    ///     reading the other.
    /// </remarks>
    [Fact]
    public void TheDocumentStillSaysWhatThisClassAsserts() {
        var document = Path.Combine(CliRunner.RepositoryRoot, "docs", "plan", "09-quality-gates-and-reporting.md");
        var text = File.ReadAllText(document);

        // Anti-vacuity: a document that moved, or a section that was renamed, must fail loudly
        // rather than let every assertion below pass over an empty string.
        Assert.Contains("### Exit codes", text, StringComparison.Ordinal);

        var expected = new (int Code, string Meaning)[] {
            (0, "gate passed"), (1, "gate failed"), (2, "formatting changes needed"), (3, "configuration error"),
            (4, "load failure"), (5, "internal error"), (130, "cancelled")
        };

        foreach (var (code, meaning) in expected) {
            var row = new Regex($@"^\|\s*{code}\s*\|\s*(?<meaning>[^|]+?)\s*\|\s*$", RegexOptions.Multiline);
            var match = row.Match(text);

            Assert.True(match.Success, $"docs/plan/09 § 'Exit codes' has no row for {code}.");
            Assert.Contains(meaning, match.Groups["meaning"].Value, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     ⚠ The third copy of the table: the MSBuild targets, which decide from an exit code whether
    ///     a consumer's build has a finding.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The class remarks say the table existed twice and the two disagreed. It existed three
    ///         times. <c>Tools/Rikarin.Skala.MSBuild/build/Rikarin.Skala.MSBuild.targets</c> keyed its
    ///         finding diagnostic on exit <b>1</b> for every mode — right for <c>SkalaMode=check</c>,
    ///         wrong for the two format verbs, whose finding is <b>2</b>. So the default mode, which is
    ///         the mode every consumer gets without asking, never once said "these files are not
    ///         formatted": it fell through to the "could not complete" branch and reported the tool as
    ///         broken instead, and <c>SkalaTreatFindingsAsErrors</c> could not fail a build whatever the
    ///         tree looked like.
    ///     </para>
    ///     <para>
    ///         ⚠ This reads the shipped file rather than a copy of its numbers, for the same reason the
    ///         test above reads the document: a constant compared against another constant cannot fail
    ///         when both are wrong together. The only thing that catches this end to end is
    ///         <c>.github/scripts/install-smoke-test.sh</c>, which needs a pack, a feed and a global
    ///         install; it did catch it, and then it stayed red for a release because nothing cheaper
    ///         ever asked the question.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheMsBuildTargetsAgreeAboutWhichCodeIsAFinding() {
        var targets = Path.Combine(
            CliRunner.RepositoryRoot,
            "Tools",
            "Rikarin.Skala.MSBuild",
            "build",
            "Rikarin.Skala.MSBuild.targets"
        );

        var text = File.ReadAllText(targets);

        // Anti-vacuity: a renamed property must fail here rather than let every match below come
        // back empty and pass.
        Assert.Contains("_SkalaFindingExit", text, StringComparison.Ordinal);

        var assignments = new Regex(
            """<_SkalaFindingExit Condition="(?<condition>[^"]*)">(?<code>\d+)</_SkalaFindingExit>""",
            RegexOptions.None
        );

        var byCode = assignments.Matches(text)
            .ToDictionary(
                static match => match.Groups["condition"].Value,
                static match => int.Parse(match.Groups["code"].Value, CultureInfo.InvariantCulture),
                StringComparer.Ordinal
            );

        // `off` is the default and its verb is the formatting check, so its finding is 2. It is
        // also the mode with no `SkalaMode` set at all, which is why the condition carries both.
        var format = Assert.Single(byCode, static entry => entry.Key.Contains("'off'", StringComparison.Ordinal));
        Assert.Contains("'$(SkalaMode)' == ''", format.Key, StringComparison.Ordinal);
        Assert.Equal(2, format.Value);

        // `check` runs the gate, whose finding is 1.
        var check = Assert.Single(byCode, static entry => entry.Key.Contains("'check'", StringComparison.Ordinal));
        Assert.Equal(1, check.Value);

        // ⚠ And the switch that started it: not global, so it may only be handed to the verb that
        // has it. Anything else is a parse error, which the ladder reads as "the tool broke".
        var common = new Regex("""<_SkalaCommon Condition="(?<condition>[^"]*)">(?<value>[^<]*)</_SkalaCommon>""");
        foreach (Match match in common.Matches(text)) {
            if (!match.Groups["value"].Value.Contains("no-color", StringComparison.Ordinal)) {
                continue;
            }

            Assert.Contains("'check'", match.Groups["condition"].Value, StringComparison.Ordinal);
        }
    }
}
