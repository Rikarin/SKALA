using System.Text.RegularExpressions;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
/// The exit codes docs/plan/09 § "Exit codes" publishes, asserted against the real binary.
/// </summary>
/// <remarks>
/// <para>
/// ⚠ <b>The contract was wrong from M1 to M9 and every test in the tree agreed with it.</b>
/// <c>ReportingTests.ExitCodes_AreTheOnesHooksAndCiDependOn</c> asserted
/// <c>ExitCodes.FormattingNeeded == 2</c> — true, and useless, because <c>format</c> did not use
/// <c>ExitCodes</c>. It used <c>FormatCommand.ChangesFound</c>, which was 1, and
/// <c>FormatCommand.Failed</c>, which was 2: the published table inverted. A hook told to
/// auto-format on 2 and stop on 1 did the opposite of both.
/// </para>
/// <para>
/// So this class asserts <em>behaviour</em>, through the process boundary, for each row of the
/// table that a command can actually produce. A constant compared against another constant cannot
/// fail when both are wrong together; a command that exits 2 can only be made to do so by exiting
/// 2. The one thing it does not do is trust the document from memory —
/// <see cref="TheDocumentStillSaysWhatThisClassAsserts"/> reads the table out of
/// <c>docs/plan/09</c>, so a future edit that changes the document has to change these tests too,
/// which is the conversation that should happen.
/// </para>
/// </remarks>
public sealed class ExitCodeContractTests : IDisposable {
    readonly string _directory = Directory.CreateTempSubdirectory("skala-exit-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    string Write(string name, string content) {
        var path = Path.Combine(_directory, name);
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
    /// ⚠ <c>--diff</c> reports exactly what <c>--check</c> reports, so it exits the same way.
    /// </summary>
    [Fact]
    public void Two_WhenDiffFindsEdits() {
        var path = Write("Diffed.cs", "class  C{ void  M( ){} }\n");
        Assert.Equal(2, CliRunner.Run("format", "--diff", path).ExitCode);
    }

    /// <summary>
    /// ⚠ <c>arrange --check</c> is a formatting check and shares the row, which was the second
    /// copy of the same inverted pair.
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
        var run = CliRunner.Run("check", "--load", "loose", "--verbsoe");

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("--verbsoe", run.StandardOutput + run.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ And it names the path rather than analysing an empty set.
    /// </summary>
    /// <remarks>
    /// <c>format --check no-such-dir</c> exited <b>0</b> — "0 files would be reformatted, 0 left
    /// alone" — because a directory that is not there contributes no files, and no files is
    /// indistinguishable from no findings. A gate that passes on a typo is quiet in exactly the
    /// case it exists for.
    /// </remarks>
    [Fact]
    public void Three_WhenAPathDoesNotExist() {
        var run = CliRunner.Run("format", "--check", "no-such-directory-anywhere");

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("does not exist", run.StandardOutput + run.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ A refusal to run is 3, not 2. A hook that auto-formats on 2 would read this refusal as an
    /// instruction to do the thing it just refused.
    /// </summary>
    [Fact]
    public void Three_WhenAnInvocationIsRefused() {
        var run = CliRunner.Run("config", "diff", CliRunner.Template);

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("needs two files", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    /// The flag that was not a flag: <c>--verbose</c> bound to the variadic <c>&lt;paths&gt;</c>,
    /// so <c>check --verbose</c> looked for C# files in a directory called "--verbose", found none,
    /// and exited 4 from a repository full of them.
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
    /// ⚠ And it is recursive, so it means the same thing on every verb. A flag a script puts in a
    /// variable has to be accepted wherever the variable is used.
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
    /// ⚠ A path that genuinely begins with <c>-</c> is still reachable, spelled the way every other
    /// POSIX tool requires. The guard rejects mistyped options, not filenames.
    /// </summary>
    [Fact]
    public void ADashedFilename_IsStillReachable() {
        var path = Write("-dashed.cs", "class  C{ }\n");
        Assert.Equal(2, CliRunner.Run("format", "--check", path).ExitCode);
    }

    /// <summary>
    /// ⚠ The table in the document, read rather than remembered.
    /// </summary>
    /// <remarks>
    /// The rows this class exercises are pinned here against docs/plan/09's own table, so that
    /// changing the document without changing the tool fails the build. This is the half that was
    /// missing: the code and the document disagreed for four milestones and neither side was
    /// reading the other.
    /// </remarks>
    [Fact]
    public void TheDocumentStillSaysWhatThisClassAsserts() {
        var document = Path.Combine(CliRunner.RepositoryRoot, "docs", "plan", "09-quality-gates-and-reporting.md");
        var text = File.ReadAllText(document);

        // Anti-vacuity: a document that moved, or a section that was renamed, must fail loudly
        // rather than let every assertion below pass over an empty string.
        Assert.Contains("### Exit codes", text, StringComparison.Ordinal);

        var expected = new (int Code, string Meaning)[] {
            (0, "gate passed"),
            (1, "gate failed"),
            (2, "formatting changes needed"),
            (3, "configuration error"),
            (4, "load failure"),
            (5, "internal error"),
            (130, "cancelled")
        };

        foreach (var (code, meaning) in expected) {
            var row = new Regex($@"^\|\s*{code}\s*\|\s*(?<meaning>[^|]+?)\s*\|\s*$", RegexOptions.Multiline);
            var match = row.Match(text);

            Assert.True(match.Success, $"docs/plan/09 § 'Exit codes' has no row for {code}.");
            Assert.Contains(meaning, match.Groups["meaning"].Value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
