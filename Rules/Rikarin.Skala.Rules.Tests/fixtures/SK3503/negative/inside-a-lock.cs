using System.IO;
using System.Threading.Tasks;

public sealed class Guarded {
    readonly object _gate = new object();

    // `await` inside a lock body is CS1996, so there is no rewrite available and therefore no
    // finding — the same boundary SK3002 respects.
    public async Task WriteAsync(Stream target) {
        lock (_gate) {
            using (var writer = new StreamWriter(target)) {
                writer.WriteLine("done");
            }
        }

        await Task.Yield();
    }
}
