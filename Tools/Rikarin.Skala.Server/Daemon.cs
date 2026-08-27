using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Net.Sockets;
using Rikarin.Skala.Core;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Protocol;

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
    NamedPipeServerStream? _pipe;
    DateTime _lastRequest = DateTime.UtcNow;

    public Daemon(string repositoryRoot) {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _socketPath = DaemonProtocol.SocketPath(_repositoryRoot);
    }

    public string SocketPath => _socketPath;

    /// <summary>
    /// Binds the transport. ⚠ Throws if one is already bound, which is how two daemons are prevented.
    /// </summary>
    /// <remarks>
    /// ⚠ Two transports, chosen by platform: a named pipe on Windows and a Unix domain socket
    /// everywhere else. See <see cref="DaemonTransport"/> for why the Windows half exists now and
    /// did not before.
    /// </remarks>
    public void Listen() {
        // ⚠ Creates `.skala/` *and* leaves the self-ignore marker in it. The daemon is the most
        // common way `.skala/` first appears in somebody's tree, because it is started lazily by
        // the first single-file format — so it is the most important one to keep out of git status.
        SkalaDirectory.EnsureForFile(_socketPath);

        if (DaemonTransport.UsesNamedPipe) {
            // ⚠ A pipe name is machine-global, so "already bound" is reported by the OS when the
            // first instance exists. There is no stale-file case to recover from — which is the one
            // genuine advantage the pipe has over the socket.
            _pipe = CreatePipe();
            return;
        }

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

    NamedPipeServerStream CreatePipe() =>
        new(
            DaemonProtocol.PipeName(_repositoryRoot),
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
        );

    public async Task RunAsync(CancellationToken cancellation) {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _stopping.Token);
        var idle = IdleWatchdog(linked.Token);

        try {
            while (!linked.IsCancellationRequested) {
                if (DaemonTransport.UsesNamedPipe) {
                    // ⚠ One instance per connection: a NamedPipeServerStream that has been connected
                    // cannot be reused, so the accepted one is handed to the handler and the next is
                    // created here. Getting this wrong serialises every client behind the first.
                    var accepted = _pipe!;
                    await accepted.WaitForConnectionAsync(linked.Token).ConfigureAwait(false);
                    _pipe = CreatePipe();
                    _ = ServeAsync(accepted, linked.Token);
                    continue;
                }

                var connection = await _listener!.AcceptAsync(linked.Token).ConfigureAwait(false);
                _ = ServeAsync(new NetworkStream(connection, ownsSocket: true), linked.Token);
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

    async Task ServeAsync(Stream connection, CancellationToken cancellation) {
        using var stream = connection;
        try {
            while (!cancellation.IsCancellationRequested) {
                var request = await DaemonProtocol.ReadRequestAsync(stream, cancellation).ConfigureAwait(false);
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
        // ⚠ Windows never reaches here: it binds a named pipe with PipeOptions.CurrentUserOnly,
        // which is the ACL, and there is no socket file to chmod. Everywhere else, 0600 — the
        // socket is a command channel into the developer's own tree.
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
        if (_pipe is not null) {
            await _pipe.DisposeAsync().ConfigureAwait(false);
        }

        _stopping.Dispose();

        // ⚠ A pipe leaves nothing on disk to unlink; the OS reclaims the name when the last handle
        // closes. Only the socket transport has a file to clean up.
        if (DaemonTransport.UsesNamedPipe) {
            return;
        }

        try {
            if (File.Exists(_socketPath)) {
                File.Delete(_socketPath);
            }
        } catch (IOException) {
            // Leaving a stale socket file behind is recoverable; Listen() probes before unlinking.
        }
    }
}
