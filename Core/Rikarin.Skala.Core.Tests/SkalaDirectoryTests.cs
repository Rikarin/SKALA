using Rikarin.Skala.Core;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
/// The tool must not dirty the repository it runs on.
/// </summary>
/// <remarks>
/// ⚠ These exist because it did. <c>.skala/cache/</c> was found inside a reference checkout after a
/// measurement run, and the daemon leaves a socket in every repository it is ever started in. The
/// contract is one line — the directory carries a <c>.gitignore</c> containing <c>*</c> — and every
/// writer goes through <see cref="SkalaDirectory"/> so that a seventh call site cannot forget it.
/// </remarks>
public sealed class SkalaDirectoryTests : IDisposable {
    readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skala-hygiene-" + Guid.NewGuid().ToString("n")[..12]
    );

    public SkalaDirectoryTests() => Directory.CreateDirectory(_root);

    public void Dispose() {
        try {
            Directory.Delete(_root, recursive: true);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void Ensure_WritesTheSelfIgnoreMarker() {
        SkalaDirectory.Ensure(_root);

        var marker = Path.Combine(_root, ".skala", ".gitignore");
        Assert.True(File.Exists(marker), "`.skala/` was created without the marker that hides it.");
        Assert.Equal("*", File.ReadAllText(marker).Trim());
    }

    [Fact]
    public void Ensure_WithSegments_CreatesTheSubdirectoryAndStillMarksTheRoot() {
        var created = SkalaDirectory.Ensure(_root, "cache");

        Assert.True(Directory.Exists(created));
        Assert.EndsWith("cache", created, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_root, ".skala", ".gitignore")));
    }

    /// <summary>
    /// ⚠ The cache, the clone index, the SARIF report and the daemon socket all hold a full file
    /// path rather than the repository root. That shape has to mark the directory too, or four of
    /// the six writers silently do not.
    /// </summary>
    [Fact]
    public void EnsureForFile_MarksTheNearestSkalaAbove() {
        var file = Path.Combine(_root, ".skala", "cache", "loose.diagnostics.json");

        SkalaDirectory.EnsureForFile(file);

        Assert.True(Directory.Exists(Path.GetDirectoryName(file)!));
        Assert.True(File.Exists(Path.Combine(_root, ".skala", ".gitignore")));
    }

    /// <summary>
    /// ⚠ A report the user redirected with <c>--output</c> is theirs. Dropping a `.gitignore` beside
    /// it would be the same discourtesy this type exists to prevent.
    /// </summary>
    [Fact]
    public void EnsureForFile_OutsideSkala_WritesNoMarker() {
        var file = Path.Combine(_root, "reports", "mine.sarif");

        SkalaDirectory.EnsureForFile(file);

        Assert.True(Directory.Exists(Path.Combine(_root, "reports")));
        Assert.False(File.Exists(Path.Combine(_root, "reports", ".gitignore")));
    }

    [Fact]
    public void Mark_DoesNotOverwriteAMarkerTheUserEdited() {
        var skala = SkalaDirectory.Ensure(_root);
        var marker = Path.Combine(skala, ".gitignore");
        File.WriteAllText(marker, "# mine\n*\n!keep.txt\n");

        SkalaDirectory.Mark(skala);

        Assert.Contains("!keep.txt", File.ReadAllText(marker), StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_IsIdempotent() {
        SkalaDirectory.Ensure(_root, "cache");
        SkalaDirectory.Ensure(_root, "cache");

        Assert.Single(Directory.GetFiles(Path.Combine(_root, ".skala")));
    }

    /// <summary>
    /// The property the marker exists for, asserted against real git rather than against the file's
    /// contents: a repository that has had Skala run in it reports nothing in <c>git status</c>.
    /// </summary>
    [Fact]
    public void ASkalaDirectory_IsInvisibleToGitStatus() {
        if (!Git(_root, "init", "-q")) {
            return; // No git on this machine; the other tests still cover the marker's contents.
        }

        File.WriteAllText(Path.Combine(_root, "a.txt"), "seed\n");
        Git(_root, "add", "-A");
        Git(_root, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-q", "-m", "seed");

        SkalaDirectory.Ensure(_root, "cache");
        File.WriteAllText(Path.Combine(_root, ".skala", "report.sarif"), "{}");
        File.WriteAllText(Path.Combine(_root, ".skala", "cache", "x.json"), "[]");

        Assert.Equal(string.Empty, GitStatus(_root).Trim());
    }

    static bool Git(string directory, params string[] arguments) {
        try {
            var info = new System.Diagnostics.ProcessStartInfo("git") {
                WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in arguments) {
                info.ArgumentList.Add(argument);
            }

            using var process = System.Diagnostics.Process.Start(info);
            if (process is null) {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        } catch (System.ComponentModel.Win32Exception) {
            return false;
        }
    }

    static string GitStatus(string directory) {
        var info = new System.Diagnostics.ProcessStartInfo("git") {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add("status");
        info.ArgumentList.Add("--porcelain");
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
