using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rikarin.Skala.Server;

/// <summary>What a client asks the daemon to do.</summary>
public sealed record DaemonRequest {
    /// <summary>Exact match, no negotiation. See <see cref="DaemonProtocol.Version"/>.</summary>
    public string Version { get; init; } = DaemonProtocol.Version;

    /// <summary><c>format</c>, <c>status</c> or <c>stop</c>.</summary>
    public string Command { get; init; } = "status";

    public string? Path { get; init; }

    /// <summary>The file's current text, when the client has it in hand (an editor buffer).</summary>
    public string? Text { get; init; }

    public IReadOnlyList<KeyValuePair<string, string>> Overrides { get; init; } = [];
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
/// The daemon's wire format: a 4-byte big-endian length, then UTF-8 JSON.
/// </summary>
/// <remarks>
/// ⚠ Private, and versioned by <em>exact match</em> (docs/plan/11 § "The daemon"): a client that
/// meets a daemon of another version kills it and starts its own. There is no negotiation and no
/// compatibility window, because the two halves ship in one package and a daemon that half-speaks an
/// older protocol is a source of formatting differences between two developers on one repository —
/// which is the failure the whole tool exists to prevent.
/// </remarks>
public static class DaemonProtocol {
    /// <summary>Bumped whenever <see cref="DaemonRequest"/> or <see cref="DaemonResponse"/> changes.</summary>
    public const string Version = "skala/1";

    /// <summary>The socket lives beside the crash artefacts, under the repository root.</summary>
    public const string SocketName = "daemon.sock";

    /// <summary>⚠ The daemon exits after this long without a request. It is not a service.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>⚠ A cap, so that a corrupt length prefix cannot ask for a gigabyte.</summary>
    public const int MaxFrame = 64 * 1024 * 1024;

    public static string SocketPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, ".skala", SocketName);

    public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellation) {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellation).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellation).ConfigureAwait(false);
        await stream.FlushAsync(cancellation).ConfigureAwait(false);
    }

    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellation) {
        var header = new byte[4];
        if (!await FillAsync(stream, header, cancellation).ConfigureAwait(false)) {
            return default;
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 0 or > MaxFrame) {
            throw new InvalidDataException($"frame length {length.ToString(System.Globalization.CultureInfo.InvariantCulture)} is out of range");
        }

        var payload = new byte[length];
        if (!await FillAsync(stream, payload, cancellation).ConfigureAwait(false)) {
            return default;
        }

        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(payload), Json);
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
