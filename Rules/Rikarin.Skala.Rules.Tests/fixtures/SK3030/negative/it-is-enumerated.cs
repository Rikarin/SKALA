using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class Pipeline {
    public async Task RunAsync() {
        await foreach (var item in ProcessAsync()) {
            System.Console.WriteLine(item);
        }
    }

    static async IAsyncEnumerable<int> ProcessAsync() {
        await Task.Yield();
        yield return 1;
    }
}
