using System.IO;
using System.Threading.Tasks;

public sealed class Passthrough {
    // The resource leaks — that is SK3501's question — but nothing disposes it before the task
    // completes, which is this rule's.
    public Task<string> ReadAsync(string path) {
        var reader = new StreamReader(path);
        return reader.ReadToEndAsync();
    }
}
