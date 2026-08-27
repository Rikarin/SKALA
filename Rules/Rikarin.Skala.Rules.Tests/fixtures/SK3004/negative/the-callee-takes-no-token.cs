using System.Threading;
using System.Threading.Tasks;

public sealed class Formatter {
    public async Task RunAsync(CancellationToken cancellationToken) {
        await RenderAsync("report");
        Describe("report");
    }

    static Task RenderAsync(string name) => Task.CompletedTask;

    static string Describe(string name) => name;
}
