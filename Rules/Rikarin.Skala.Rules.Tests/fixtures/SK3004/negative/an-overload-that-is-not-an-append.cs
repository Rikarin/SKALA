using System.Threading;
using System.Threading.Tasks;

public sealed class Sender {
    // ⚠ There *is* an overload taking a token, and it is not this one with a token appended: it
    // drops the retry count as well. Appending an argument would call a different method than the
    // one the finding was reported against, so the rule says nothing.
    public async Task RunAsync(CancellationToken cancellationToken) {
        await SendAsync("payload", 3);
    }

    static Task SendAsync(string body, int retries) => Task.CompletedTask;

    static Task SendAsync(string body, CancellationToken cancellationToken) => Task.CompletedTask;
}
