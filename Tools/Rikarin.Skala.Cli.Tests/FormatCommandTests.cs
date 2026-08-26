using System.Diagnostics;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
/// <c>skala format</c> through the real binary: argument parsing, exit codes and the text a user
/// sees are the only part of the tool that is a contract (ADR-010).
/// </summary>
public sealed class FormatCommandTests : IDisposable {
    readonly string _directory = Directory.CreateTempSubdirectory("skala-format-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    string Write(string name, string content) {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Format_WritesTheFile() {
        // ⚠ Multi-line on purpose. `csharp_preserve_single_line_blocks = true` and milestone 1
        // never adds a break, so a one-line class stays one line — correctly.
        var path = Write("A.cs", "class C{\nvoid M(){\nM();\n}\n}\n");
        var run = CliRunner.Run("format", path);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("class C {\n    void M() {\n        M();\n    }\n}\n", File.ReadAllText(path));
    }

    [Fact]
    public void Check_WritesNothingAndExitsOne() {
        const string source = "class C{void M(){M();}}\n";
        var path = Write("B.cs", source);
        var run = CliRunner.Run("format", "--check", path);

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(source, File.ReadAllText(path));
        Assert.Contains("1 file would be reformatted", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_ExitsZero_WhenThereIsNothingToDo() {
        var path = Write("C.cs", "class C {\n    void M() {\n        M();\n    }\n}\n");
        var run = CliRunner.Run("format", "--check", path);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("0 files would be reformatted", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_PrintsAUnifiedDiffAndWritesNothing() {
        // ⚠ Without `--check` too. `--diff` is a reporting mode on its own (docs/plan/04 § "Emitting
        // minimal edits"); until M2 it printed the diff and then wrote the file, which is a flag
        // people reach for on a tree they do not own.
        const string source = "class C{void M(){M();}}\n";
        var path = Write("D.cs", source);
        var run = CliRunner.Run("format", "--diff", path);

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(source, File.ReadAllText(path));
        Assert.Contains("@@", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("-class C{void M(){M();}}", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("+class C {", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Range_AppliesOnlyTheEditsThatIntersectIt() {
        const string source = "class C {\n    void M( ) {\n    }\n\n    void N( ) {\n    }\n}\n";
        var path = Write("E.cs", source);
        var second = source.IndexOf("void N", StringComparison.Ordinal);

        var run = CliRunner.Run("format", $"--range={second}:{source.Length}", path);
        Assert.Equal(0, run.ExitCode);

        var formatted = File.ReadAllText(path);
        Assert.Contains("void M( ) {", formatted, StringComparison.Ordinal);
        Assert.Contains("void N() {", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Option_OverridesTheConfiguration() {
        var path = Write("F.cs", "class C{\nvoid M(){\nM();\n}\n}\n");
        CliRunner.Run("format", "--option", "resharper_csharp_indent_size=2", path);
        Assert.Contains("\n  void M()", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void NotParseable_IsReportedAndLeftAlone() {
        const string source = "class C { void M( {\n";
        var path = Write("G.cs", source);
        var run = CliRunner.Run("format", path);

        Assert.Equal(source, File.ReadAllText(path));
        Assert.Contains("SK9010", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Staged_RefusesWhenAStagedFileAlsoHasUnstagedChanges() {
        // ⚠ The case that is easy to get wrong in a way that loses uncommitted work: formatting the
        // worktree copy would stage edits the author did not mean to commit.
        Git("init", "-q", ".");
        Git("config", "user.email", "a@b");
        Git("config", "user.name", "s");
        var path = Write("H.cs", "class C{}\n");
        Git("add", "H.cs");
        File.WriteAllText(path, "class C{}\nclass D{}\n");

        var run = Run("format", "--staged", ".");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("unstaged changes", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--staged=worktree", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("class C{}\nclass D{}\n", File.ReadAllText(path));
    }

    [Fact]
    public void StagedWorktree_FormatsAndStagesAnyway() {
        Git("init", "-q", ".");
        Git("config", "user.email", "a@b");
        Git("config", "user.name", "s");
        var path = Write("I.cs", "class C{}\n");
        Git("add", "I.cs");
        File.WriteAllText(path, "class C{}\nclass D{}\n");

        var run = Run("format", "--staged=worktree", ".");
        Assert.Equal(0, run.ExitCode);
        Assert.Equal("class C { }\n\nclass D { }\n", File.ReadAllText(path));

        // Both the worktree and the index, which is the whole point for a pre-commit hook.
        Assert.Empty(Git("diff", "--name-only").Trim());
    }

    CliRun Run(params string[] arguments) {
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = _directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        start.ArgumentList.Add(CliRunner.Assembly);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliRun(process.ExitCode, output, error);
    }

    string Git(params string[] arguments) {
        var start = new ProcessStartInfo("git") {
            WorkingDirectory = _directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
