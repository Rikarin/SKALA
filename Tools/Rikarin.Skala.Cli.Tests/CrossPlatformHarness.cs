using Rikarin.Skala.Testing;
using System.Diagnostics;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     A scratch repository the cross-platform tests drive the real binary inside.
/// </summary>
/// <remarks>
///     ⚠ Every one of doc 12 § "Cross-platform"'s hazards is about what a path or a byte looks like on
///     disk, so all of them have to be asserted through the process rather than through a library call:
///     a unit test of <c>SarifWriter.Relative</c> cannot see that the repository root the CLI computed
///     disagreed with the paths the loader produced, and a unit test of the line-ending option cannot
///     see that the writer opened the file in text mode. <see cref="CliRunner" /> runs from the
///     repository root; these tests need their own working directory, their own <c>.editorconfig</c>
///     and their own <c>.git</c>, so they carry their own runner.
/// </remarks>
public sealed class CrossPlatformScratch : IDisposable {
    public CrossPlatformScratch(string prefix) {
        Root = Directory.CreateTempSubdirectory(prefix).FullName;
    }

    public string Root { get; }

    public void Dispose() {
        try {
            Directory.Delete(Root, recursive: true);
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // ⚠ A long-path tree is exactly the thing Windows cannot always delete, and a test that
            // fails in teardown reports the wrong defect.
        }
    }

    public void InitialiseGit() {
        // The repository root is what makes a SARIF path repo-relative, and it is found by walking
        // up to a `.git`.
        Git("init", "-q", ".");
        Git("config", "user.email", "a@b");
        Git("config", "user.name", "s");
    }

    public string WriteBytes(string relativePath, byte[] content) {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    public string WriteText(string relativePath, string content) {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // ⚠ `File.WriteAllText`, not a writer with a newline setting: the test supplies the exact
        // bytes it means, because the whole subject is which bytes end a line.
        File.WriteAllText(path, content);
        return path;
    }

    public CliRun Run(params string[] arguments) => Start("dotnet", [CliRunner.Assembly, .. arguments]);

    public string Git(params string[] arguments) => Start("git", arguments).StandardOutput;

    CliRun Start(string executable, IEnumerable<string> arguments) {
        var start = new ProcessStartInfo(executable) {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"{executable} did not start.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, output, error);
    }
}
