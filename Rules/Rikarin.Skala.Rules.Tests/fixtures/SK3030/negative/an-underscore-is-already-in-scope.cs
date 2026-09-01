// CS0136: the rewrite's loop variable would shadow this local, so the finding goes with its fix.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class Pipeline {
    public async Task RunAsync() {
        var _ = 0;
        Console.WriteLine(_);
        ProcessAsync();
        await Task.Yield();
    }

    static async IAsyncEnumerable<int> ProcessAsync() {
        await Task.Yield();
        yield return 1;
    }
}
