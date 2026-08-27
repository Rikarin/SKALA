using System.Threading;
using System.Threading.Tasks;

public sealed class Pipeline {
    // The parameter's name is not part of the pattern: what matters is that exactly one
    // `CancellationToken` is in scope, and the fix writes whatever it is called.
    public async Task RunAsync(CancellationToken stopping) {
        await StepAsync("first");
    }

    static Task StepAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
