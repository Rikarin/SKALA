using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class Retrier {
    public async Task RunAsync(CancellationToken cancellationToken) {
        try {
            await WorkAsync(cancellationToken);
        } catch (InvalidOperationException) {
            await ReportAsync();
        }
    }

    static Task WorkAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    static Task ReportAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
