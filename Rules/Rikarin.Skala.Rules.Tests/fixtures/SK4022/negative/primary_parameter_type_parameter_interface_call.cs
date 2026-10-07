// ⚠ Without a `struct` constraint the same call is still constrained, and when `T` is a struct it
// mutates the capture today and a copy after `readonly` (#412). Only `T : class` would make it safe.
public interface ICounter {
    int Count { get; }

    void Increment();
}

public struct Counter : ICounter {
    int count;

    public readonly int Count => count;

    public void Increment() => count++;
}

struct Tally<T>(T counter)
    where T : ICounter {
    public void Bump() => counter.Increment();

    public int Count => counter.Count;
}

public static class Probe {
    public static int Run() {
        var tally = new Tally<Counter>(new Counter());
        tally.Bump();
        return tally.Count;
    }
}
