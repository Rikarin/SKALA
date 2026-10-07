using System;

// ⚠ #412's audit: a `using` local is read-only, so each `Increment()` would run on a defensive copy
// and the count read afterwards would stay 0. Every read here hands back an int or nothing, so only the
// struct itself decides it.
public struct Counter : IDisposable {
    public int Count;

    public void Increment() => Count++;

    public void Dispose() { }
}

public static class Probe {
    public static int Run() {
        var counter = new Counter();
        counter.Increment();
        counter.Increment();
        var count = counter.Count;
        return count;
    }
}
