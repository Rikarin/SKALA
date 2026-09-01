using System;
using System.Threading;

public sealed class Gate : IDisposable {
    readonly SemaphoreSlim semaphore = new(1, 1);

    public void Enter() => semaphore.Wait();

    public void Dispose() {
    }
}
