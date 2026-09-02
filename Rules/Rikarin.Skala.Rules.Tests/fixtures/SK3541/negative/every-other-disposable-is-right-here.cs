using System.IO;
using System.Net.Http;

// The rest of the family's subject. A `using` around a stream, a request or a response is the
// correct shape and stays correct; only the client's lifetime is inverted.
public sealed class Ordinary {
    public long Measure(byte[] payload) {
        using var buffer = new MemoryStream(payload);
        using var request = new HttpRequestMessage();
        using var content = new ByteArrayContent(payload);
        return buffer.Length + (request.Version.Major + content.Headers.ContentLength).GetValueOrDefault();
    }
}
