using System.Diagnostics;
using Rikarin.Skala.Protocol;

namespace Rikarin.Skala.Server;

/// <summary>
/// The full tool's copy of the thin half: connect, send, receive.
/// </summary>
/// <remarks>
/// ⚠ <b>This is no longer the hot path.</b> Before M7 it was, and it could never meet its budget:
/// this type lives in an assembly that references Rikarin.Skala.Formatting.CSharp, so reaching it
/// meant loading Roslyn, and <c>skala daemon status</c> — doing no work at all — measured 95 ms
/// against a 40 ms budget for the whole operation. The hook path is now
/// <c>Tools/Rikarin.Skala.Client</c>, a NativeAOT binary that references only
/// Rikarin.Skala.Protocol. This copy remains for the full tool's own use (<c>skala format</c> run
/// directly, <c>daemon status</c>, <c>daemon stop</c>) and shares the wire format with the client
/// rather than reimplementing it.
/// <para>
/// ⚠ Every failure here is a fallback, never an error. A daemon that is not running, is running a
/// different protocol version, or has died mid-request must produce the same output as
/// <c>SKALA_NO_DAEMON=1</c> and not a diagnostic; the daemon is an optimisation and an optimisation
/// that can fail a build is not one.
/// </remarks>
public static class DaemonClient {
    /// <summary>Whether something is listening. Used before unlinking a socket file.</summary>
    public static bool Probe(string repositoryRoot) =>
        Send(repositoryRoot, new DaemonRequest { Command = "status" }) is { Ok: true };

    /// <summary>Sends one request, or null when the daemon is unreachable.</summary>
    public static DaemonResponse? Send(string repositoryRoot, DaemonRequest request, TimeSpan? timeout = null) {
        var root = Path.GetFullPath(repositoryRoot);
        var budget = timeout ?? TimeSpan.FromSeconds(5);

        using var stream = DaemonTransport.Connect(root, budget);
        if (stream is null) {
            return null;
        }

        try {
            using var cancellation = new CancellationTokenSource(budget);
            DaemonProtocol.WriteAsync(stream, request, cancellation.Token).GetAwaiter().GetResult();
            return DaemonProtocol.ReadResponseAsync(stream, cancellation.Token).GetAwaiter().GetResult();
        } catch (IOException) {
            return null;
        } catch (OperationCanceledException) {
            return null;
        } catch (InvalidDataException) {
            return null;
        }
    }

    /// <summary>Whether the daemon should be used at all. <c>SKALA_NO_DAEMON=1</c> and <c>--no-daemon</c>.</summary>
    public static bool Enabled =>
        !string.Equals(Environment.GetEnvironmentVariable("SKALA_NO_DAEMON"), "1", StringComparison.Ordinal);

    static int _started;

    /// <summary>
    ///     Starts a daemon for <paramref name="repositoryRoot" /> in the background and returns
    ///     immediately. ⚠ It does not wait for it and the caller does not use it: this run stays cold
    ///     and does its own work, and the *next* one finds a socket. Waiting here would put the daemon's
    ///     own start — parse, JIT, the first configuration resolution — inside the budget the daemon
    ///     exists to meet, which is the shape that makes lazy starting feel slower than no daemon at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ At most once per process, and only when the running executable is one that can host a
    ///     daemon. Under `dotnet run`, under a test host, or wherever else the formatter is being used
    ///     as a library, <see cref="Environment.ProcessPath" /> is somebody else's program and
    ///     re-launching it with `daemon run` would start something nobody asked for. Every failure is
    ///     silent, for the reason in the type's remarks.
    /// </remarks>
    public static void StartInBackground(string repositoryRoot) {
        if (!Enabled || Interlocked.Exchange(ref _started, 1) != 0) {
            return;
        }

        var executable = Environment.ProcessPath;
        if (executable is null || !HostsADaemon(executable)) {
            return;
        }

        Spawn(executable, repositoryRoot);
    }

    /// <summary>
    ///     ⚠ <c>skala</c> is the NativeAOT client after M7 and cannot host a daemon — it has no Roslyn.
    ///     <c>skala-tool</c> is the full tool. Accepting both keeps a development build (where the full
    ///     tool is still called <c>skala</c>) working alongside a published layout.
    /// </summary>
    public static bool HostsADaemon(string executable) {
        var name = Path.GetFileNameWithoutExtension(executable);
        return string.Equals(name, "skala-tool", StringComparison.Ordinal)
            || string.Equals(name, "skala", StringComparison.Ordinal);
    }

    internal static void Spawn(string executable, string repositoryRoot) {
        try {
            using var process = Process.Start(
                new ProcessStartInfo(executable) {
                    ArgumentList = { "daemon", "run" },
                    WorkingDirectory = Path.GetFullPath(repositoryRoot),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                }
            );
        } catch (System.ComponentModel.Win32Exception) {
            // No daemon, and the caller has already fallen through to doing the work itself.
        } catch (InvalidOperationException) { } catch (IOException) { }
    }
}
