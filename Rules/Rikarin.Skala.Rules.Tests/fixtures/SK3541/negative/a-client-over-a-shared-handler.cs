using System.Net.Http;
using System.Threading.Tasks;

// The documented mitigation, and the one shape the rule must not report: the sockets belong to
// the shared handler, and the thin wrapper above it costs nothing to dispose.
public static class Pooled {
    static readonly SocketsHttpHandler Handler = new();

    public static async Task<string> Fetch(string url) {
        using var client = new HttpClient(Handler, disposeHandler: false);
        return await client.GetStringAsync(url).ConfigureAwait(false);
    }
}
