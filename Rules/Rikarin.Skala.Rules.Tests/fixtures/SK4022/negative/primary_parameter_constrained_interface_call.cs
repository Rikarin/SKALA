// ⚠ An interface member called on a `T : struct` capture is a constrained call on the capture
// itself; no interface member is `readonly`, so the fix would redirect it to a copy (#412).
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
    where T : struct, ICounter {
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
