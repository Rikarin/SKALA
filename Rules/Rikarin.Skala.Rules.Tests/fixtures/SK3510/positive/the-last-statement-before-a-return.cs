using System;

// The explicit call is the last thing the scope does before returning a local, so the `using`'s own
// disposal follows it with nothing in between, and an idempotent `Dispose` sees no difference.
public sealed class Handle : IDisposable {
    public int Disposals;

    public void Dispose() {
        if (Disposals == 0) {
            Disposals++;
        }
    }
}

public static class Probe {
    public static int Run() {
        var outer = new Handle();
        var seen = Use(outer);
        return seen * 10 + outer.Disposals;
    }

    static int Use(Handle outer) {
        using var handle = outer;
        var seen = handle.Disposals;
        handle.Dispose();
        return seen;
    }
}
