using System.IO;
using System.Threading.Tasks;

public sealed class Writer {
    // ⚠ `return await x;` is CS1997 in an `async Task` method, so the repair here drops the
    // `return` — which is only equivalent because falling off the end is what the `return` did.
    public Task WriteAsync(string path, string text) {
        using (var writer = new StreamWriter(path)) {
            return writer.WriteAsync(text);
        }
    }
}
