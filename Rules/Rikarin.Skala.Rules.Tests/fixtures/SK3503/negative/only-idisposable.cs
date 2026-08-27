using System;
using System.Threading.Tasks;

public sealed class Scope : IDisposable {
    public void Dispose() { }
}

public sealed class Runner {
    // No `DisposeAsync` to reach, so there is nothing to prefer and nothing to report.
    public async Task RunAsync() {
        using (var scope = new Scope()) {
            await Task.Yield();
            GC.KeepAlive(scope);
        }
    }
}
