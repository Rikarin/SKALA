using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class Pipeline {
    public async Task RunAsync() {
        Process();
        await Task.Yield();
    }

    static IEnumerable<int> Process() {
        yield return 1;
    }
}
