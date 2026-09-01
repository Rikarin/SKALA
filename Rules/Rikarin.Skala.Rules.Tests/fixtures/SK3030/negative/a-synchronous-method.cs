// The same bug, and the repair makes the method `async` and changes every caller. That is a refactor
// rather than an edit, and it is the line SK3503 draws in the same place.

using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class Pipeline {
    public void Run() {
        ProcessAsync();
    }

    static async IAsyncEnumerable<int> ProcessAsync() {
        await Task.Yield();
        yield return 1;
    }
}
