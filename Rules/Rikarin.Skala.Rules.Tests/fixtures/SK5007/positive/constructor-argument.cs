using System.IO;
using System.Net.Security;

// The `SslStream` constructor takes the callback directly, so the finding is on an argument rather
// than on an assignment.
public static class Transport {
    public static SslStream Wrap(Stream inner) =>
        new SslStream(inner, false, (_, _, _, _) => true);
}
