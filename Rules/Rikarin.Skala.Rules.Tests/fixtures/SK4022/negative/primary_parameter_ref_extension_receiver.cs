// ⚠ A `this ref` extension takes the capture by writable reference, which `readonly struct` rejects:
// the receiver is a `ref` argument with no `ref` keyword at the call site (#412).
public struct Counter {
    public int Count;
}

public static class CounterExtensions {
    public static void Increment(this ref Counter counter) => counter.Count++;
}

struct Tally(Counter counter) {
    public void Bump() => counter.Increment();

    public int Count => counter.Count;
}
