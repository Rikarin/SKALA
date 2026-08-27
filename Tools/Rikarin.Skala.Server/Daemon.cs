using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Server;

/// <summary>
/// The per-repository daemon: a unix domain socket, a format cache, and an idle timer.
/// </summary>
/// <remarks>
/// docs/plan/11 § "The daemon". Three properties it is built to keep, in the order they matter:
/// <list type="number">
/// <item>
/// ⚠ <b>Every command works identically with <c>SKALA_NO_DAEMON=1</c>.</b> The daemon is only
/// allowed to make things faster, so it holds the results of the same
/// <see cref="CSharpFormatter"/> the CLI calls and never a second implementation of anything.
/// </item>
/// <item>
/// ⚠ <b>It never watches the filesystem.</b> It is asked; it does not observe. Every answer is
/// keyed on the content it was computed from, so there is no invalidation to get wrong and no
/// window in which a stale answer is served.
/// </item>
/// <item>
/// ⚠ <b>It exits rather than lingers.</b> Thirty minutes idle and it is gone; a version mismatch
/// and the client kills it. A daemon that outlives its usefulness is a daemon that gets blamed for
/// the next thing that goes wrong.
/// </item>
/// </list>
/// </remarks>
public sealed class Daemon : IAsyncDisposable {
    readonly string _repositoryRoot;
    readonly string _socketPath;
    readonly FormatService _service = new();
    readonly Stopwatch _uptime = Stopwatch.StartNew();
    readonly CancellationTokenSource _stopping = new();
    Socket? _listener;
    DateTime _lastRequest = DateTime.UtcNow;

    public Daemon(string repositoryRoot) {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _socketPath = DaemonProtocol.SocketPath(_repositoryRoot);
    }

    public string SocketPath => _socketPath;

    /// <summary>Binds the socket. ⚠ Throws if one is already bound, which is how two daemons are prevented.</summary>
    public void Listen() {
        Directory.CreateDirectory(Path.GetDirectoryName(_socketPath)!);

        // ⚠ A socket file left by a crashed daemon is not a running daemon. Probing it before
        // unlinking is the difference between recovering and stealing a live daemon's socket.
        if (File.Exists(_socketPath)) {
            if (DaemonClient.Probe(_repositoryRoot)) {
                throw new IOException($"a daemon is already listening on {_socketPath}");
            }

            File.Delete(_socketPath);
        }

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(16);

        // ⚠ 0600. The socket is a command channel into the developer's own tree.
        Restrict(_socketPath);
    }

    public async Task RunAsync(CancellationToken cancellation) {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _stopping.Token);
        var idle = IdleWatchdog(linked.Token);

        try {
            while (!linked.IsCancellationRequested) {
                var connection = await _listener!.AcceptAsync(linked.Token).ConfigureAwait(false);
                _ = ServeAsync(connection, linked.Token);
            }
        } catch (OperationCanceledException) {
            // The idle timer or the caller asked; both are ordinary shutdowns.
        } finally {
            await idle.ConfigureAwait(false);
        }
    }

    async Task IdleWatchdog(CancellationToken cancellation) {
        try {
            while (!cancellation.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellation).ConfigureAwait(false);
                if (DateTime.UtcNow - _lastRequest > DaemonProtocol.IdleTimeout) {
                    await _stopping.CancelAsync().ConfigureAwait(false);
                    return;
                }
            }
        } catch (OperationCanceledException) {
            // Shutting down.
        }
    }

    async Task ServeAsync(Socket connection, CancellationToken cancellation) {
        using var stream = new NetworkStream(connection, ownsSocket: true);
        try {
            while (!cancellation.IsCancellationRequested) {
                var request = await DaemonProtocol.ReadAsync<DaemonRequest>(stream, cancellation).ConfigureAwait(false);
                if (request is null) {
                    return;
                }

                _lastRequest = DateTime.UtcNow;
                var response = Handle(request);
                await DaemonProtocol.WriteAsync(stream, response, cancellation).ConfigureAwait(false);

                if (string.Equals(request.Command, "stop", StringComparison.Ordinal)) {
                    await _stopping.CancelAsync().ConfigureAwait(false);
                    return;
                }
            }
        } catch (IOException) {
            // A client that went away mid-request is not the daemon's problem.
        } catch (OperationCanceledException) {
            // Shutting down.
        }
    }

    DaemonResponse Handle(DaemonRequest request) {
        // ⚠ Exact match. A client of another version kills this daemon and starts its own; there is
        // nothing to negotiate, because two Skala versions formatting one repository is the failure
        // the version pinning in docs/plan/11 § "Distribution" exists to prevent.
        if (!string.Equals(request.Version, DaemonProtocol.Version, StringComparison.Ordinal)) {
            return new DaemonResponse {
                Ok = false,
                Error = $"protocol mismatch: daemon speaks {DaemonProtocol.Version}, client speaks {request.Version}"
            };
        }

        switch (request.Command) {
            case "status":
                return new DaemonResponse {
                    Status = string.Create(
                        CultureInfo.InvariantCulture,
                        $"up {_uptime.Elapsed.TotalSeconds:F0}s, {_service.Held} documents held, {_service.Hits} hits, {_service.Misses} misses"
                    )
                };

            case "stop":
                return new DaemonResponse { Status = "stopping" };

            case "format":
                if (request.Path is not { Length: > 0 } path) {
                    return new DaemonResponse { Ok = false, Error = "format needs a path" };
                }

                try {
                    var result = _service.Format(
                        path,
                        request.Text,
                        request.Overrides,
                        Path.Combine(_repositoryRoot, ".skala"),
                        request.Define
                    );

                    return new DaemonResponse {
                        Formatted = result.Formatted,
                        Changed = result.Changed,
                        Diagnostics = [.. result.Diagnostics.Select(static d => d.ToString())]
                    };
                } catch (IOException exception) {
                    return new DaemonResponse { Ok = false, Error = exception.Message };
                }

            default:
                return new DaemonResponse { Ok = false, Error = $"unknown command '{request.Command}'" };
        }
    }

    static void Restrict(string path) {
        // ⚠ Windows has no mode to set; a named pipe carries its own ACL and the socket file there
        // is not the security boundary. Everywhere else, 0600: the socket is a command channel into
        // the developer's own tree.
        if (OperatingSystem.IsWindows()) {
            return;
        }

        try {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        } catch (IOException) {
            // A socket the platform will not chmod is still better than no daemon.
        } catch (UnauthorizedAccessException) {
            // Likewise.
        }
    }

    public async ValueTask DisposeAsync() {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener?.Dispose();
        _stopping.Dispose();

        try {
            if (File.Exists(_socketPath)) {
                File.Delete(_socketPath);
            }
        } catch (IOException) {
            // Leaving a stale socket file behind is recoverable; Listen() probes before unlinking.
        }
    }
}
