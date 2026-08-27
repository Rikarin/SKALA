using System;
using System.IO;
using System.Threading.Tasks;

public sealed class Deferred {
    // ⚠ A lambda is a body boundary. The enclosing method being `async` does not make an `await`
    // legal inside a synchronous delegate, and writing one there is CS4034.
    public async Task RunAsync(Stream target) {
        Action write = () => {
            using (var writer = new StreamWriter(target)) {
                writer.WriteLine("done");
            }
        };

        write();
        await Task.Yield();
    }
}
