using System.Threading;
using System.Threading.Tasks;

public sealed class Backoff {
    public async Task WaitAsync(CancellationToken cancellationToken) {
        await Task.Delay(250);
    }
}
