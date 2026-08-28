using System.Diagnostics;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     A second copy of the built tool, which can be rebuilt underneath the daemon running from it.
/// </summary>
/// <remarks>
///     ⚠ A copy and not the build output itself, for the obvious reason: the sabotage rewrites an
///     assembly, and rewriting the one the test host is running from breaks every other test in the
///     run. It also has to be a <i>separate</i> install from the one the <c>skala format</c> under test
///     is invoked from — a client launched out of the sabotaged directory would fail to load the
///     formatter and the test would prove nothing about the daemon.
/// </remarks>
sealed class SecondInstall : IDisposable {
    public SecondInstall() {
        var source = Path.GetDirectoryName(CliRunner.Assembly)!;
        Directory = Path.Combine(Path.GetTempPath(), "skala-install-" + Guid.NewGuid().ToString("n")[..8]);
        Copy(source, Directory);
        Tool = Path.Combine(Directory, Path.GetFileName(CliRunner.Assembly));
    }

    public string Directory { get; }

    /// <summary>The <c>skala-tool.dll</c> of the copy, to be run through the muxer.</summary>
    public string Tool { get; }

    /// <summary>
    ///     A rebuild that changed the formatter: <c>Rikarin.Skala.Formatting.CSharp.dll</c> gets a new
    ///     module identity. ⚠ Only that assembly moves, and the entry point does not — which is the
    ///     shape the real defect had, and the reason an identity that watches only <c>skala-tool.dll</c>
    ///     would miss it.
    /// </summary>
    public void RebuildTheFormatter() =>
        File.Copy(
            Path.Combine(Directory, "Rikarin.Skala.Core.dll"),
            Path.Combine(Directory, "Rikarin.Skala.Formatting.CSharp.dll"),
            overwrite: true
        );

    static void Copy(string from, string to) {
        System.IO.Directory.CreateDirectory(to);
        foreach (var directory in System.IO.Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) {
            System.IO.Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        }

        foreach (var file in System.IO.Directory.GetFiles(from, "*", SearchOption.AllDirectories)) {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
        }
    }

    public void Dispose() {
        try {
            System.IO.Directory.Delete(Directory, recursive: true);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>
///     ⚠ <b>The end-to-end half of the stale-daemon regression.</b> A real daemon process, a real
///     install rewritten underneath it, and the real <c>skala format</c> command asking it a question.
/// </summary>
/// <remarks>
///     The defect: a daemon serves output from the build it was launched with for ever. The only
///     compatibility check was <c>DaemonProtocol.Version</c>, a <em>wire</em> version, which does not
///     move when the formatter does; <c>DaemonProtocol.IdleTimeout</c> is thirty minutes but every
///     request refreshes it, so an actively used stale daemon never dies. Reproduced by hand before
///     the fix: with a live daemon, an edit to <c>CSharpFormatter</c> and a rebuild,
///     <c>skala format --diff</c> indented with four spaces and the same command under
///     <c>SKALA_NO_DAEMON=1</c> indented with seven, from the same binary, in the same second.
///     <para>
///         ⚠ The assertion that fails against the code before the fix is the last one: the daemon was
///         still up, and had just answered.
///     </para>
/// </remarks>
public sealed class StaleDaemonTests {
    [Fact]
    public void ADaemonWhoseBuildChanges_StopsRatherThanServe() {
        using var install = new SecondInstall();
        using var repository = new BareRepository();

        var daemon = Start(install.Tool, repository.Root, "daemon", "run");
        try {
            WaitUntilListening(repository.Root);

            var before = Status(repository.Root);
            Assert.StartsWith("up ", before, StringComparison.Ordinal);

            // ⚠ Prove the daemon is actually answering before believing anything that follows. Every
            // path here falls back to doing the work in-process, so a daemon that is never consulted
            // looks exactly like a daemon that is working.
            var served = CliRunner.Run("format", "--check", repository.Subject);
            Assert.Equal(2, served.ExitCode);
            // Misses rather than hits: this is the first format of the file, so the daemon computed
            // the answer instead of finding it. Either counter moving proves it was asked.
            Assert.DoesNotContain(" 0 misses,", Status(repository.Root), StringComparison.Ordinal);

            // The cheap half of the fix: `daemon status` now identifies the build it is serving.
            Assert.Contains("build ", before, StringComparison.Ordinal);
            Assert.DoesNotContain("STALE", before, StringComparison.Ordinal);

            install.RebuildTheFormatter();

            // ⚠ The failure mode has to be safe and quiet, not a hard error. This is a pre-commit
            // hook: the command still does the work, still writes nothing it should not, and still
            // reports the same exit code — it just does it cold, out of the caller's own build.
            var after = CliRunner.Run("format", "--check", repository.Subject);
            Assert.Equal(2, after.ExitCode);
            Assert.Equal(served.StandardOutput, after.StandardOutput);

            // ⚠ And the daemon is gone, so the *next* command gets a fresh one rather than the same
            // wrong answer for the next thirty minutes. Before the fix this read "up …s, …": the
            // daemon had just served the request out of a formatter that no longer existed.
            Assert.StartsWith("no daemon", Status(repository.Root), StringComparison.Ordinal);
            Assert.True(daemon.WaitForExit(15_000), "the daemon refused the request but did not exit");
        } finally {
            Kill(daemon);
        }
    }

    static void WaitUntilListening(string root) {
        for (var i = 0; i < 200 && Status(root).StartsWith("no daemon", StringComparison.Ordinal); i++) {
            Thread.Sleep(50);
        }
    }

    /// <summary>
    ///     ⚠ Asked through the <em>pristine</em> build and not the sabotaged copy, which cannot start:
    ///     the copy's formatter assembly has been replaced and every command out of it fails to load.
    ///     That costs nothing, because the status line is composed by the daemon — the client only
    ///     prints what came back over the socket.
    /// </summary>
    static string Status(string root) => CliRunner.Run("daemon", "status", root).StandardOutput.Trim();

    static Process Start(string tool, string workingDirectory, params string[] arguments) {
        var info = new ProcessStartInfo("dotnet") {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        info.ArgumentList.Add(tool);
        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        return Process.Start(info)!;
    }

    static void Kill(Process process) {
        try {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
        } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
    }
}

/// <summary>
///     A throwaway repository with one unformatted file.
/// </summary>
/// <remarks>
///     ⚠ Short root, for <c>DaemonProtocol.SocketPath</c>'s 104-byte cap — same reason as
///     <see cref="DaemonBed" />, and a bed that exercises the overflow fallback every time is a bed
///     that never exercises the documented path. ⚠ A <c>.git</c> directory rather than
///     <c>git init</c>: both the client and <c>DaemonUse</c> locate a daemon by walking up for
///     <c>.git</c>, and that is all they look at.
/// </remarks>
sealed class BareRepository : IDisposable {
    public BareRepository() {
        Root = Path.Combine(
            OperatingSystem.IsWindows() ? Path.GetTempPath() : "/tmp",
            "skr" + Guid.NewGuid().ToString("n")[..6]
        );

        Directory.CreateDirectory(Path.Combine(Root, ".git"));
        Subject = Path.Combine(Root, "A.cs");
        File.WriteAllText(Subject, "class C{void M(){M();}}\n");
    }

    public string Root { get; }

    public string Subject { get; }

    public void Dispose() {
        try {
            Directory.Delete(Root, recursive: true);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
