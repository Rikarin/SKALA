using System.Threading;
using System.Threading.Tasks;

public sealed class Auditor {
    // ⚠ Writing `CancellationToken.None` is how an author says this call is deliberately not
    // cancellable. A rule that replaced it would be arguing with a decision rather than finding an
    // omission.
    public async Task RecordAsync(CancellationToken cancellationToken) {
        await WriteAsync("start", CancellationToken.None);
        await WriteAsync("stop", default);
    }

    static Task WriteAsync(string entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
