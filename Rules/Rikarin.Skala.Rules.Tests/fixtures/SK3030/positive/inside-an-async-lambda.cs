using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class Pipeline {
    public Func<Task> Build() {
        return async () => {
            ProcessAsync();
            await Task.Yield();
        };
    }

    static async IAsyncEnumerable<int> ProcessAsync() {
        await Task.Yield();
        yield return 1;
    }
}
