using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Net.Sockets;
using Rikarin.Skala.Core;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Protocol;

namespace Rikarin.Skala.Server;

/// <summary>
///     The per-repository daemon: a unix domain socket, a format cache, and an idle timer.
/// </summary>
/// <remarks>
///     docs/plan/11 § "The daemon". Three properties it is built to keep, in the order they matter:
///     <list type="number">
///         <item>
///             ⚠ <b>Every command works identically with <c>SKALA_NO_DAEMON=1</c>.</b> The daemon is only
///             allowed to make things faster, so it holds the results of the same
///             <see cref="CSharpFormatter" /> the CLI calls and never a second implementation of anything.
///         </item>
///         <item>
///             ⚠ <b>It never watches the filesystem.</b> It is asked; it does not observe. Every answer is
///             keyed on the content it was computed from, so there is no invalidation to get wrong and no
///             window in which a stale answer is served.
///         </item>
///         <item>
///             ⚠ <b>It exits rather than lingers.</b> Thirty minutes idle and it is gone; a version mismatch
///             and the client kills it; and — since <see cref="BuildIdentity" /> — a rebuild underneath it and
///             it stops rather than serve the old formatter's bytes. A daemon that outlives its usefulness is
///             a daemon that gets blamed for the next thing that goes wrong.
///         </item>
///     </list>
/// </remarks>
public sealed class Daemon : IAsyncDisposable {
    readonly string _repositoryRoot;
    readonly string _socketPath;
    readonly FormatService _service = new();
    readonly RetainedCompilations _compilations = new();
    readonly MemoryPolicy _memory = new();
    readonly BuildIdentity _build;
    readonly Stopwatch _uptime = Stopwatch.StartNew();
    readonly CancellationTokenSource _stopping = new();
    Socket? _listener;
    NamedPipeServerStream? _pipe;
    DateTime _lastRequest = DateTime.UtcNow;
    int _unlinked;

    /// <summary>
    ///     ⚠ <paramref name="build" /> is injectable for one reason: the regression test has to be able
    ///     to change the build under a running daemon, and it cannot rewrite the assemblies the test host
    ///     itself has loaded. Production always passes null and gets <see cref="BuildIdentity.Current" />.
    /// </summary>
    public Daemon(string repositoryRoot, BuildIdentity? build = null) {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _socketPath = DaemonProtocol.SocketPath(_repositoryRoot);
        _build = build ?? BuildIdentity.Current;
    }

    public string SocketPath => _socketPath;

    /// <summary>
    ///     Binds the transport. ⚠ Throws if one is already bound, which is how two daemons are prevented.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two transports, chosen by platform: a named pipe on Windows and a Unix domain socket
    ///     everywhere else. See <see cref="DaemonTransport" /> for why the Windows half exists now and
    ///     did not before.
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

        // ⚠ docs/plan/13 § "Memory": drop, then drop again, then exit rather than swap. A daemon
        // that pushes a laptop into swap is worse than no daemon, and the failure is silent and
        // blamed on the editor. Exiting is safe because every command works identically with
        // SKALA_NO_DAEMON=1 and the lazy start brings a fresh daemon back.
        var memory = MemoryWatchdog(linked.Token);

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
            // The idle timer, the memory policy or the caller asked; all are ordinary shutdowns.
        } finally {
            await idle.ConfigureAwait(false);
            await memory.ConfigureAwait(false);
        }
    }

    /// <summary>Whether the daemon stopped because it was holding too much rather than being idle.</summary>
    public bool StoppedForMemory { get; private set; }

    /// <summary>
    ///     Whether the daemon stopped because Skala was rebuilt underneath it. See
    ///     <see cref="BuildIdentity" />.
    /// </summary>
    public bool StoppedForStaleBuild { get; private set; }

    async Task MemoryWatchdog(CancellationToken cancellation) {
        if (await _memory.WatchAsync(_service, _compilations, cancellation).ConfigureAwait(false)) {
            StoppedForMemory = true;
            await _stopping.CancelAsync().ConfigureAwait(false);
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
        // ⚠ `await using`, not `using`. A `NamedPipeServerStream` or a `NetworkStream` disposed
        // synchronously flushes on the calling thread; in an async method that is the thread pool
        // thread this connection was serving on. SK3503 is Skala's own rule, on Skala's own daemon.
        await using var stream = connection;
        try {
            while (!cancellation.IsCancellationRequested) {
                var request = await DaemonProtocol.ReadRequestAsync(stream, cancellation).ConfigureAwait(false);
                if (request is null) {
                    return;
                }

                _lastRequest = DateTime.UtcNow;
                var response = Handle(request);
                await DaemonProtocol.WriteAsync(stream, response, cancellation).ConfigureAwait(false);

                // ⚠ The stale-build stop is here and not in `Handle`, for the same reason `stop` is:
                // the answer has to reach the client before the daemon starts tearing itself down, or
                // the client sees a closed socket instead of the refusal and cannot tell a rebuilt
                // formatter from a crashed daemon.
                if (StoppedForStaleBuild || string.Equals(request.Command, "stop", StringComparison.Ordinal)) {
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

        // ⚠ The protocol version above is a *wire* version and says nothing about the build. A daemon
        // whose formatter has been rebuilt underneath it answers every `format` with the old build's
        // bytes for ever — the idle timer is thirty minutes but each request refreshes it — and the
        // only symptom is output that disagrees with `--no-daemon`. See `BuildIdentity`.
        //
        // ⚠ It gates `format` and nothing else. `status` has to be able to *report* a stale daemon
        // without being the thing that kills it, and `stop` must always work.
        if (string.Equals(request.Command, "format", StringComparison.Ordinal) && _build.HasChanged()) {
            var current = _build.OnDisk();
            StoppedForStaleBuild = true;

            // ⚠ Unlinked before the answer is written, not in DisposeAsync afterwards. The client's
            // reaction to this refusal is to start a fresh daemon (`DaemonClient.StartInBackground`),
            // and that one probes for a socket file before binding: leave the file in place for the
            // few milliseconds this one takes to shut down and the replacement refuses to start.
            Unlink();

            return new DaemonResponse {
                Ok = false,
                Error = $"stale daemon: it is serving build {_build.Loaded} and {current} is on disk. "
                    + "It has stopped rather than answer with a formatter that no longer exists; "
                    + "the next command starts a fresh one."
            };
        }

        switch (request.Command) {
            case "status":
                return new DaemonResponse {
                    // ⚠ The held bytes and the working set are in here because doc 13's RSS budget
                    // is a number somebody has to be able to read without a profiler. A daemon whose
                    // memory can only be inspected by attaching to it is a daemon whose memory
                    // nobody inspects.
                    Status = string.Create(
                        CultureInfo.InvariantCulture,
                        $"up {_uptime.Elapsed.TotalSeconds:F0}s, {_service.Held} documents held ({_service.Bytes / (1024 * 1024)} MB), "
                        + $"{_service.Hits} hits, {_service.Misses} misses, {_service.Evictions} evicted, "
                        + $"{_compilations.Held} compilation(s), RSS {Environment.WorkingSet / (1024 * 1024)} MB, "
                        + $"{_memory.Drops} memory drop(s), {BuildLine()}"
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

    /// <summary>
    ///     The build half of <c>daemon status</c>: what this daemon is serving, and whether that is
    ///     still what is installed.
    /// </summary>
    /// <remarks>
    ///     ⚠ Appended to the status line rather than added to <see cref="DaemonResponse" />, so that
    ///     <see cref="DaemonProtocol.Version" /> does not move: nothing about the wire shape changes,
    ///     the thin client needs no new code, and an older client talking to a newer daemon still works.
    ///     <para>
    ///         ⚠ This is the half of the fix a person uses. The automatic detection makes the wrong answer
    ///         impossible; this makes the <i>diagnosis</i> possible, which is what the forty minutes twice
    ///         over actually went on — nothing in <c>daemon status</c> identified the build, so "the daemon
    ///         is old" was never a hypothesis anyone could check.
    ///     </para>
    /// </remarks>
    string BuildLine() {
        if (!_build.Known) {
            return "build unknown";
        }

        var current = _build.OnDisk();
        return string.Equals(current, _build.Loaded, StringComparison.Ordinal)
            ? "build " + _build.Loaded
            : "build " + _build.Loaded + " (STALE, " + current + " is on disk)";
    }

    /// <summary>
    ///     Removes the socket file, at most once.
    /// </summary>
    /// <remarks>
    ///     ⚠ At most once, and that is the whole point of the guard. The stale-build path unlinks early
    ///     so its replacement can bind; if <see cref="DisposeAsync" /> then unlinked again it would
    ///     delete the <i>replacement's</i> socket — a live daemon with no name, unreachable for ever,
    ///     which is a worse failure than the one being fixed.
    /// </remarks>
    void Unlink() {
        if (DaemonTransport.UsesNamedPipe || Interlocked.Exchange(ref _unlinked, 1) != 0) {
            return;
        }

        try {
            if (File.Exists(_socketPath)) {
                File.Delete(_socketPath);
            }
        } catch (IOException) {
            // Leaving a stale socket file behind is recoverable; Listen() probes before unlinking.
        } catch (UnauthorizedAccessException) { }
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
        // closes. Only the socket transport has a file to clean up — and only if the stale-build path
        // has not already done it.
        Unlink();
    }
}
