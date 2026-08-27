using System.IO.Pipes;
using System.Net.Sockets;

namespace Rikarin.Skala.Protocol;

/// <summary>
///     How a client reaches a daemon: a Unix domain socket, or a named pipe on Windows.
/// </summary>
/// <remarks>
///     ⚠ <b>The named-pipe half did not exist before M7.</b> <c>Daemon.Restrict</c> carried a comment
///     saying "Windows has no mode to set; a named pipe carries its own ACL" — describing a design
///     nobody had written. Both ends constructed <c>AddressFamily.Unix</c> unconditionally, so
///     doc 12 § "Cross-platform"'s "named-pipe daemon transport" hazard had nothing to test, and the
///     daemon's behaviour on a repository that is not on a local NTFS volume was undefined.
///     <para>
///         The choice is by platform and not by probing, because a probe that falls back turns a Windows
///         configuration problem into a silent 60 ms tax on every hook invocation, which is precisely the
///         failure mode doc 13 says is "silent and blamed on the editor".
///     </para>
/// </remarks>
public static class DaemonTransport {
    /// <summary>Named pipes on Windows, Unix domain sockets everywhere else.</summary>
    public static bool UsesNamedPipe => OperatingSystem.IsWindows();

    /// <summary>
    ///     Whether a daemon appears to exist for this repository, cheaply and without connecting.
    ///     ⚠ On Unix this is a stat of the socket file; on Windows a pipe has no file, so this is
    ///     always true and the connect attempt is the real test. Both are advisory — the caller must
    ///     treat a failed connect as "no daemon" rather than as an error.
    /// </summary>
    public static bool MightExist(string repositoryRoot) =>
        UsesNamedPipe || File.Exists(DaemonProtocol.SocketPath(repositoryRoot));

    /// <summary>
    ///     Connects to the daemon for <paramref name="repositoryRoot" />, or returns null.
    /// </summary>
    /// <remarks>
    ///     ⚠ Never throws for "there is no daemon". Every caller is on a path where the daemon is an
    ///     optimisation, and an optimisation that can fail a build is not one.
    /// </remarks>
    public static Stream? Connect(string repositoryRoot, TimeSpan timeout) {
        try {
            if (UsesNamedPipe) {
                var pipe = new NamedPipeClientStream(
                    ".",
                    DaemonProtocol.PipeName(repositoryRoot),
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous
                );

                // ⚠ Milliseconds, and short. A pipe that is not there fails immediately; a long
                // timeout only ever delays the fallback that is about to happen anyway.
                pipe.Connect((int)timeout.TotalMilliseconds);
                return pipe;
            }

            var path = DaemonProtocol.SocketPath(repositoryRoot);
            if (!File.Exists(path)) {
                return null;
            }

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(path));
            return new NetworkStream(socket, ownsSocket: true);
        } catch (TimeoutException) {
            return null;
        } catch (SocketException) {
            return null;
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        }
    }
}
