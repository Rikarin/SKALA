using System.Threading;
using System.Threading.Tasks;

public sealed class Coordinator {
    // ⚠ Two tokens is not a harder case, it is a different one: which of them an inner call should
    // get is a decision about intent. The rule does not pick.
    public async Task RunAsync(CancellationToken shutdown, CancellationToken request) {
        await WorkAsync();
    }

    static Task WorkAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
