using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Consumer {
    // ⚠ The inner `handle` has the same name as the outer `using` variable and is a different
    // symbol. A rule that matched on the identifier would delete a disposal nothing else performs.
    public void Report(bool nested) {
        using var handle = new Handle();
        if (nested) {
            var inner = new Handle();
            inner.Dispose();
        }

        GC.KeepAlive(handle);
    }
}
