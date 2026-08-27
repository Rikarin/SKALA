using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class Canceller {
    // A `CancellationTokenSource` is `IDisposable` and nothing more, so `Dispose()` is the whole of
    // its cleanup and there is no asynchronous path to prefer.
    public async Task RunAsync() {
        var source = new CancellationTokenSource();
        await Task.Delay(1, source.Token);
        source.Dispose();
    }
}
