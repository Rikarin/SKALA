using System.Threading;
using System.Threading.Tasks;

public sealed class Pipeline {
    public async Task RunAsync(CancellationToken cancellationToken) {
        await StepAsync("first", cancellationToken: cancellationToken);
    }

    static Task StepAsync(string name, int retries = 0, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
