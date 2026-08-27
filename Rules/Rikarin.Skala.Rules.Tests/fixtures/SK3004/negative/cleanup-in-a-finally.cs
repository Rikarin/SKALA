using System.Threading;
using System.Threading.Tasks;

public sealed class Session {
    // Cleanup a cancellation can abort is worse than cleanup that ignores one, so a `finally` — and
    // a `catch` — are where not forwarding the token is the right call.
    public async Task RunAsync(CancellationToken cancellationToken) {
        try {
            await WorkAsync(cancellationToken);
        } finally {
            await ReleaseAsync();
        }
    }

    static Task WorkAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    static Task ReleaseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
