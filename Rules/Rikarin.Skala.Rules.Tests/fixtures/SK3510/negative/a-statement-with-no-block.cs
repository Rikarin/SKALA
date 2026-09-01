using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Consumer {
    // ⚠ The same redundancy, withheld: deleting the statement would leave `if (early)` with no
    // body, and a fix that does not parse is the one failure a fixing tool may not have.
    public void Report(bool early) {
        using var handle = new Handle();
        if (early) handle.Dispose();
    }
}
