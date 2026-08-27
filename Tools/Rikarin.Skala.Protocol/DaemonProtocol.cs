using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rikarin.Skala.Protocol;

/// <summary>What a client asks the daemon to do.</summary>
public sealed record DaemonRequest {
    /// <summary>Exact match, no negotiation. See <see cref="DaemonProtocol.Version" />.</summary>
    public string Version { get; init; } = DaemonProtocol.Version;

    /// <summary><c>format</c>, <c>status</c> or <c>stop</c>.</summary>
    public string Command { get; init; } = "status";

    public string? Path { get; init; }

    /// <summary>The file's current text, when the client has it in hand (an editor buffer).</summary>
    public string? Text { get; init; }

    public IReadOnlyList<KeyValuePair<string, string>> Overrides { get; init; } = [];

    /// <summary>
    ///     The preprocessor symbols the file is parsed with (<c>--define</c>, or a loaded compilation's).
    /// </summary>
    /// <remarks>
    ///     ⚠ Part of the request rather than of the daemon's own state. Two clients of one repository
    ///     may hold different symbol sets — a `#if DEBUG` file formatted for a Debug compilation and the
    ///     same file formatted for Release are different answers — so the symbols travel with the
    ///     question and are part of the cache key, never a property of the daemon.
    /// </remarks>
    public IReadOnlyList<string> Define { get; init; } = [];
}

/// <summary>What the daemon answers.</summary>
public sealed record DaemonResponse {
    public string Version { get; init; } = DaemonProtocol.Version;

    public bool Ok { get; init; } = true;

    public string? Error { get; init; }

    /// <summary>The formatted text, for <c>format</c>.</summary>
    public string? Formatted { get; init; }

    /// <summary>Whether formatting produced any edit at all.</summary>
    public bool Changed { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    /// <summary>For <c>status</c>: how long the daemon has been up, and what it is holding.</summary>
    public string? Status { get; init; }
}

/// <summary>
///     ⚠ Source-generated serialisation, because the thin client is NativeAOT and reflection-based
///     <c>JsonSerializer</c> does not survive trimming. This is also why the protocol types are plain
///     records with no polymorphism: the generator has to be able to see the whole shape.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(DaemonRequest))]
[JsonSerializable(typeof(DaemonResponse))]
public sealed partial class DaemonJson : JsonSerializerContext;

/// <summary>
///     The daemon's wire format: a 4-byte big-endian length, then UTF-8 JSON.
/// </summary>
/// <remarks>
///     ⚠ Private, and versioned by <em>exact match</em> (docs/plan/11 § "The daemon"): a client that
///     meets a daemon of another version kills it and starts its own. There is no negotiation and no
///     compatibility window, because the two halves ship in one package and a daemon that half-speaks an
///     older protocol is a source of formatting differences between two developers on one repository —
///     which is the failure the whole tool exists to prevent.
/// </remarks>
public static class DaemonProtocol {
    /// <summary>Bumped whenever <see cref="DaemonRequest" /> or <see cref="DaemonResponse" /> changes.</summary>
    public const string Version = "skala/2";

    /// <summary>The socket lives beside the crash artefacts, under the repository root.</summary>
    public const string SocketName = "daemon.sock";

    /// <summary>⚠ The daemon exits after this long without a request. It is not a service.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    /// <summary>⚠ A cap, so that a corrupt length prefix cannot ask for a gigabyte.</summary>
    public const int MaxFrame = 64 * 1024 * 1024;

    /// <summary>
    ///     ⚠ <b>The kernel caps a Unix domain socket path at 104 bytes on macOS and 108 on Linux</b>,
    ///     and it is a hard limit in <c>struct sockaddr_un</c> rather than a policy anyone can raise.
    /// </summary>
    /// <remarks>
    ///     ⚠ This was found by measurement in M7 and it was a real, live defect: a repository checked
    ///     out anywhere deeper than about eighty-five characters — a CI workspace, a nested monorepo, a
    ///     path under <c>~/Library/…</c>, a git worktree under <c>.claude/worktrees/…</c> — produced
    ///     <c>&lt;repo&gt;/.skala/daemon.sock</c> over the cap, and <c>Daemon.Listen</c> threw
    ///     <see cref="ArgumentOutOfRangeException" />. <c>DaemonCommands.RunAsync</c> catches only
    ///     <see cref="IOException" />, so the daemon died with an unhandled exception and exit code 0,
    ///     every subsequent format silently took the cold path, and doc 13's warm row was unreachable
    ///     for those repositories with no message that said so. That is exactly the failure doc 13
    ///     § "Memory" warns about in another form: "silent, and blamed on the editor".
    ///     <para>
    ///         The socket stays in <c>.skala/</c> whenever it fits, because that is discoverable, is cleaned
    ///         up with the rest of the directory, and is what the documentation says. When it does not fit,
    ///         it moves to the system temp directory under a name hashed from the repository root — one
    ///         daemon per repository still, and per user, just not beside the repository. Both ends compute
    ///         this the same way because both ends call this method.
    ///     </para>
    /// </remarks>
    public static string SocketPath(string repositoryRoot) {
        var preferred = Path.Combine(repositoryRoot, ".skala", SocketName);

        // Bytes rather than chars: the cap is on the C string, and a non-ASCII path costs more
        // than its length. The margin below the true 104 covers the bound-file rename some
        // platforms do under the socket's own path.
        if (Encoding.UTF8.GetByteCount(preferred) <= 100) {
            return preferred;
        }

        var name = "skala-" + Hash(Path.GetFullPath(repositoryRoot)) + ".sock";
        var temporary = Path.Combine(Path.GetTempPath(), name);

        // ⚠ TMPDIR is itself long on macOS (`/var/folders/xx/…/T/`). If even that does not fit,
        // `/tmp` is the last resort — less private, but a working daemon beats none, and the
        // socket is still chmod 0600.
        return Encoding.UTF8.GetByteCount(temporary) <= 100 ? temporary : Path.Combine("/tmp", name);
    }

    /// <summary>FNV-1a. Not a security boundary — the socket's mode is — just a short, stable name.</summary>
    internal static string Hash(string value) {
        // Case-folded where the file system folds it, so two spellings of one repository are one
        // daemon. Mirrors SarifWriter.PathComparison and CacheKey.NormalisePath.
        var key = OperatingSystem.IsLinux() ? value : value.ToUpperInvariant();

        var hash = 14695981039346656037UL;
        foreach (var c in key) {
            hash = (hash ^ c) * 1099511628211UL;
        }

        return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     The Windows transport's name. ⚠ doc 12 § "Cross-platform" lists "the named-pipe daemon
    ///     transport" as a hazard needing a test, and until M7 there was nothing to test: both ends
    ///     constructed <c>AddressFamily.Unix</c> unconditionally, and only a comment in
    ///     <c>Daemon.Restrict</c> claimed otherwise. Windows 10 1803 and later do support AF_UNIX, but
    ///     the socket file must live on a local NTFS volume, which a repository on a network share or a
    ///     mapped drive is not — so the pipe is the transport there and the socket path is only ever a
    ///     name.
    ///     <para>
    ///         The pipe is named from the repository root's full path so that two repositories get two
    ///         daemons, hashed because a pipe name may not contain a backslash and is capped in length.
    ///         It is per-user: a pipe is machine-global, and two developers on one machine must not share
    ///         a daemon that holds the other's file contents.
    ///     </para>
    /// </summary>
    public static string PipeName(string repositoryRoot) =>
        "skala." + Environment.UserName + "." + Hash(Path.GetFullPath(repositoryRoot).Replace('\\', '/').TrimEnd('/'));

    public static async Task WriteAsync(Stream stream, DaemonRequest message, CancellationToken cancellation) =>
        await WriteBytesAsync(
            stream,
            JsonSerializer.SerializeToUtf8Bytes(message, DaemonJson.Default.DaemonRequest),
            cancellation
        ).ConfigureAwait(false);

    public static async Task WriteAsync(Stream stream, DaemonResponse message, CancellationToken cancellation) =>
        await WriteBytesAsync(
            stream,
            JsonSerializer.SerializeToUtf8Bytes(message, DaemonJson.Default.DaemonResponse),
            cancellation
        ).ConfigureAwait(false);

    public static async Task<DaemonRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellation) {
        var payload = await ReadBytesAsync(stream, cancellation).ConfigureAwait(false);
        return payload is null
            ? null
            : JsonSerializer.Deserialize(Encoding.UTF8.GetString(payload), DaemonJson.Default.DaemonRequest);
    }

    public static async Task<DaemonResponse?> ReadResponseAsync(Stream stream, CancellationToken cancellation) {
        var payload = await ReadBytesAsync(stream, cancellation).ConfigureAwait(false);
        return payload is null
            ? null
            : JsonSerializer.Deserialize(Encoding.UTF8.GetString(payload), DaemonJson.Default.DaemonResponse);
    }

    static async Task WriteBytesAsync(Stream stream, byte[] payload, CancellationToken cancellation) {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellation).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellation).ConfigureAwait(false);
        await stream.FlushAsync(cancellation).ConfigureAwait(false);
    }

    static async Task<byte[]?> ReadBytesAsync(Stream stream, CancellationToken cancellation) {
        var header = new byte[4];
        if (!await FillAsync(stream, header, cancellation).ConfigureAwait(false)) {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 0 or > MaxFrame) {
            throw new InvalidDataException(
                $"frame length {length.ToString(System.Globalization.CultureInfo.InvariantCulture)} is out of range"
            );
        }

        var payload = new byte[length];
        return await FillAsync(stream, payload, cancellation).ConfigureAwait(false) ? payload : null;
    }

    static async Task<bool> FillAsync(Stream stream, byte[] buffer, CancellationToken cancellation) {
        var read = 0;
        while (read < buffer.Length) {
            var got = await stream.ReadAsync(buffer.AsMemory(read), cancellation).ConfigureAwait(false);
            if (got == 0) {
                return false;
            }

            read += got;
        }

        return true;
    }
}
