namespace Rikarin.Skala.Core.Tests;

/// <summary>
///     The tool must not dirty the repository it runs on.
/// </summary>
/// <remarks>
///     ⚠ These exist because it did. <c>.skala/cache/</c> was found inside a reference checkout after a
///     measurement run, and the daemon leaves a socket in every repository it is ever started in. The
///     contract is one line — the directory carries a <c>.gitignore</c> containing <c>*</c> — and every
///     writer goes through <see cref="SkalaDirectory" /> so that a seventh call site cannot forget it.
/// </remarks>
public sealed class SkalaDirectoryTests : IDisposable {
    readonly string root = Path.Combine(
        Path.GetTempPath(),
        "skala-hygiene-" + Guid.NewGuid().ToString("n")[..12]
    );

    public SkalaDirectoryTests() {
        Directory.CreateDirectory(root);
    }

    public void Dispose() {
        try {
            Directory.Delete(root, true);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void Ensure_WritesTheSelfIgnoreMarker() {
        SkalaDirectory.Ensure(root);

        var marker = Path.Combine(root, ".skala", ".gitignore");
        Assert.True(File.Exists(marker), "`.skala/` was created without the marker that hides it.");
        Assert.StartsWith("*", File.ReadAllText(marker), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <b>The baseline is not scratch, and the marker used to swallow it.</b> docs/plan/09 calls
    ///     <c>.skala/baseline.sarif</c> "a reviewed, committed artefact — its diff in a PR is 'we
    ///     suppressed these'", and doc 09's own <c>ci</c> gate names that path. With a marker of bare
    ///     <c>*</c>, Skala wrote the file and then hid it: the first repository to adopt Skala had to
    ///     <c>git add -f</c> the one artefact the design requires be committed, and a baseline nobody
    ///     commits is a baseline the gate cannot read on the next machine.
    /// </summary>
    [Fact]
    public void TheBaseline_IsNotIgnored() {
        if (!Git(root, "init", "-q")) {
            return;
        }

        SkalaDirectory.Ensure(root);
        File.WriteAllText(Path.Combine(root, ".skala", "baseline.sarif"), "{}");

        Assert.Contains(
            "baseline.sarif",
            GitStatus(root),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ And nothing else moved. The exception is one filename, not a hole: the cache, the report,
    ///     the history and the crash reproductions are still Skala's own and still invisible.
    /// </summary>
    [Theory]
    [InlineData("report.sarif")]
    [InlineData("history.jsonl")]
    [InlineData("cache/x.json")]
    [InlineData("crash/abc123/input.cs")]
    public void EverythingElse_IsStillIgnored(string relative) {
        if (!Git(root, "init", "-q")) {
            return;
        }

        var file = Path.Combine(root, ".skala", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        SkalaDirectory.Ensure(root);
        File.WriteAllText(file, "x");

        Assert.Equal(string.Empty, GitStatus(root).Trim());
    }

    /// <summary>
    ///     ⚠ The marker keeps hiding itself. Un-ignoring it alongside the baseline would put
    ///     <c>?? .skala/.gitignore</c> in the <c>git status</c> of every repository Skala is ever run
    ///     in, which is the exact discourtesy this whole type exists to prevent.
    /// </summary>
    [Fact]
    public void TheMarker_StillHidesItself() {
        if (!Git(root, "init", "-q")) {
            return;
        }

        SkalaDirectory.Ensure(root);

        Assert.Equal(string.Empty, GitStatus(root).Trim());
    }

    /// <summary>
    ///     A repository that adopted Skala before M9 has a marker of bare <c>*</c> on disk, and
    ///     <see cref="SkalaDirectory.Mark" /> never overwrites. Without an upgrade those repositories
    ///     keep needing <c>git add -f</c> for ever.
    /// </summary>
    [Fact]
    public void Mark_UpgradesTheLegacyMarkerInPlace() {
        var skala = SkalaDirectory.Ensure(root);
        var marker = Path.Combine(skala, ".gitignore");
        File.WriteAllText(marker, "*\n");

        SkalaDirectory.Mark(skala);

        Assert.Equal(SkalaDirectory.IgnoreContents, File.ReadAllText(marker).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Ensure_WithSegments_CreatesTheSubdirectoryAndStillMarksTheRoot() {
        var created = SkalaDirectory.Ensure(root, "cache");

        Assert.True(Directory.Exists(created));
        Assert.EndsWith("cache", created, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, ".skala", ".gitignore")));
    }

    /// <summary>
    ///     ⚠ The cache, the clone index, the SARIF report and the daemon socket all hold a full file
    ///     path rather than the repository root. That shape has to mark the directory too, or four of
    ///     the six writers silently do not.
    /// </summary>
    [Fact]
    public void EnsureForFile_MarksTheNearestSkalaAbove() {
        var file = Path.Combine(root, ".skala", "cache", "loose.diagnostics.json");

        SkalaDirectory.EnsureForFile(file);

        Assert.True(Directory.Exists(Path.GetDirectoryName(file)!));
        Assert.True(File.Exists(Path.Combine(root, ".skala", ".gitignore")));
    }

    /// <summary>
    ///     ⚠ A report the user redirected with <c>--output</c> is theirs. Dropping a `.gitignore` beside
    ///     it would be the same discourtesy this type exists to prevent.
    /// </summary>
    [Fact]
    public void EnsureForFile_OutsideSkala_WritesNoMarker() {
        var file = Path.Combine(root, "reports", "mine.sarif");

        SkalaDirectory.EnsureForFile(file);

        Assert.True(Directory.Exists(Path.Combine(root, "reports")));
        Assert.False(File.Exists(Path.Combine(root, "reports", ".gitignore")));
    }

    [Fact]
    public void Mark_DoesNotOverwriteAMarkerTheUserEdited() {
        var skala = SkalaDirectory.Ensure(root);
        var marker = Path.Combine(skala, ".gitignore");
        File.WriteAllText(marker, "# mine\n*\n!keep.txt\n");

        SkalaDirectory.Mark(skala);

        Assert.Contains("!keep.txt", File.ReadAllText(marker), StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_IsIdempotent() {
        SkalaDirectory.Ensure(root, "cache");
        SkalaDirectory.Ensure(root, "cache");

        Assert.Single(Directory.GetFiles(Path.Combine(root, ".skala")));
    }

    /// <summary>
    ///     The property the marker exists for, asserted against real git rather than against the file's
    ///     contents: a repository that has had Skala run in it reports nothing in <c>git status</c>.
    /// </summary>
    [Fact]
    public void ASkalaDirectory_IsInvisibleToGitStatus() {
        if (!Git(root, "init", "-q")) {
            return; // No git on this machine; the other tests still cover the marker's contents.
        }

        File.WriteAllText(Path.Combine(root, "a.txt"), "seed\n");
        Git(root, "add", "-A");
        Git(root, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-q", "-m", "seed");

        SkalaDirectory.Ensure(root, "cache");
        File.WriteAllText(Path.Combine(root, ".skala", "report.sarif"), "{}");
        File.WriteAllText(Path.Combine(root, ".skala", "cache", "x.json"), "[]");

        Assert.Equal(string.Empty, GitStatus(root).Trim());
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

        // ⚠ `-uall`, because plain `--porcelain` collapses an untracked directory to `?? .skala/`
        // and every assertion here is about *which* file inside it is visible.
        info.ArgumentList.Add("-uall");
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
