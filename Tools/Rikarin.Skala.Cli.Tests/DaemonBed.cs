using System.Diagnostics;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     A throwaway repository with one real corpus file in it, and optionally a daemon serving it.
/// </summary>
/// <remarks>
///     ⚠ The root is deliberately <b>short</b>. A Unix domain socket path is capped at 104 bytes by the
///     kernel, and the default temp directory on macOS (<c>/var/folders/xx/…/T/</c>) plus a GUID plus
///     <c>.skala/daemon.sock</c> is already close to it. <c>DaemonProtocol.SocketPath</c> handles the
///     overflow by moving the socket, but a bed that exercises the fallback every time is a bed that
///     never exercises the documented path.
/// </remarks>
public sealed class DaemonBed : IDisposable {
    readonly Process? _daemon;

    public DaemonBed(bool startDaemon = true) {
        Root = Path.Combine(
            OperatingSystem.IsWindows() ? Path.GetTempPath() : "/tmp",
            "skb" + Guid.NewGuid().ToString("n")[..6]
        );

        Directory.CreateDirectory(Root);

        // doc 13's row is a 500-line file; take the closest real one in the corpus.
        var source = Directory
            .EnumerateFiles(
                Path.Combine(CliRunner.RepositoryRoot, "Testing", "corpus", "real"),
                "*.cs",
                SearchOption.AllDirectories
            )
            .Where(static path => !path.EndsWith(".expected.cs", StringComparison.Ordinal))
            .Select(static path => (Path: path, Lines: File.ReadAllLines(path).Length))
            .Where(static candidate => candidate.Lines is > 400 and < 700)
            .OrderBy(static candidate => candidate.Lines)
            .Select(static candidate => candidate.Path)
            .FirstOrDefault();

        Subject = Path.Combine(Root, "A.cs");
        File.WriteAllText(Subject, source is null ? "class A { }\n" : File.ReadAllText(source));

        // ⚠ A real repository, and the measurement is wrong without it. Both the thin client and
        // `DaemonUse.TryFormat` locate a daemon by walking up for `.git` — no repository, no
        // repository root, no socket to look for, and the client silently execs the full tool
        // instead of serving. The first version of this bed was a bare temp directory and the warm
        // budget measured 218 ms: not a slow daemon, a daemon that was never consulted.
        Git("init", "-q");

        if (!startDaemon || NativeLayout.Tool is not { } tool) {
            return;
        }

        var info = new ProcessStartInfo(tool) {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        info.ArgumentList.Add("daemon");
        info.ArgumentList.Add("run");
        _daemon = Process.Start(info);
    }

    public string Root { get; }

    public string Subject { get; }

    void Git(params string[] arguments) {
        try {
            var info = new ProcessStartInfo("git") {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var argument in arguments) {
                info.ArgumentList.Add(argument);
            }

            using var process = Process.Start(info);
            process?.WaitForExit(10_000);
        } catch (System.ComponentModel.Win32Exception) {
            // No git on this machine. The daemon will not be found and the budget test will say so
            // loudly rather than quietly measuring the fallback path.
        }
    }

    /// <summary>Blocks until `daemon status` answers, or gives up. Never throws.</summary>
    public void WaitUntilListening() {
        for (var i = 0; i < 100; i++) {
            if (!Status().StartsWith("no daemon", StringComparison.Ordinal)) {
                return;
            }

            Thread.Sleep(50);
        }
    }

    public string Status() {
        if (NativeLayout.Tool is not { } tool) {
            return "no daemon";
        }

        var info = new ProcessStartInfo(tool) {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };

        info.ArgumentList.Add("daemon");
        info.ArgumentList.Add("status");
        info.ArgumentList.Add(Root);

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }

    public void Dispose() {
        try {
            if (NativeLayout.Tool is { } tool) {
                var info = new ProcessStartInfo(tool) {
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
                };

                info.ArgumentList.Add("daemon");
                info.ArgumentList.Add("stop");
                info.ArgumentList.Add(Root);
                using var stop = Process.Start(info);
                stop?.WaitForExit(5000);
            }

            if (_daemon is { HasExited: false }) {
                _daemon.Kill(entireProcessTree: true);
            }

            _daemon?.Dispose();
        } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }

        try {
            Directory.Delete(Root, recursive: true);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
