using System.Net.Sockets;

namespace Rikarin.Skala.Server;

/// <summary>
/// The thin half: connect, send, receive.
/// </summary>
/// <remarks>
/// ⚠ It must stay thin. docs/plan/13 § "Startup" puts the 40 ms warm budget entirely on this side —
/// the client is what starts cold on every hook invocation, and it is a socket and a JSON writer so
/// that it can eventually be NativeAOT and start in ~5 ms. Anything it references, it pays for.
/// <para>
/// ⚠ Every failure here is a fallback, never an error. A daemon that is not running, is running a
/// different protocol version, or has died mid-request must produce the same output as
/// <c>SKALA_NO_DAEMON=1</c> and not a diagnostic; the daemon is an optimisation and an optimisation
/// that can fail a build is not one.
/// </remarks>
public static class DaemonClient {
    /// <summary>Whether something is listening. Used before unlinking a socket file.</summary>
    public static bool Probe(string repositoryRoot) => Send(repositoryRoot, new DaemonRequest { Command = "status" }) is { Ok: true };

    /// <summary>Sends one request, or null when the daemon is unreachable.</summary>
    public static DaemonResponse? Send(string repositoryRoot, DaemonRequest request, TimeSpan? timeout = null) {
        var path = DaemonProtocol.SocketPath(Path.GetFullPath(repositoryRoot));
        if (!File.Exists(path)) {
            return null;
        }

        try {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
            socket.Connect(new UnixDomainSocketEndPoint(path));
            using var stream = new NetworkStream(socket, ownsSocket: false);
            DaemonProtocol.WriteAsync(stream, request, cancellation.Token).GetAwaiter().GetResult();
            return DaemonProtocol.ReadAsync<DaemonResponse>(stream, cancellation.Token).GetAwaiter().GetResult();
        } catch (SocketException) {
            return null;
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
}
