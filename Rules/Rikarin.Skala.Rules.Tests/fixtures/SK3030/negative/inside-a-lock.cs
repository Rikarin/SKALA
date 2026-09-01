// CS1996: `await` is illegal in a `lock` body, so the rewrite would not compile.

using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class Pipeline {
    readonly object gate = new();

    public async Task RunAsync() {
        lock (gate) {
            ProcessAsync();
        }

        await Task.Yield();
    }

    static async IAsyncEnumerable<int> ProcessAsync() {
        await Task.Yield();
        yield return 1;
    }
}
