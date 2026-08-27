using System.IO;
using System.Threading.Tasks;

public sealed class Loader {
    readonly object _gate = new object();

    public async Task<int> LoadAsync(string path) {
        var length = 0;
        lock (_gate) {
            // ⚠ `await` inside a lock body is CS1996, so there is no rewrite and therefore no
            // finding. Holding a lock across a suspension is SK3008's question.
            length = File.ReadAllTextAsync(path).Result.Length;
        }

        await Task.Yield();
        return length;
    }
}
