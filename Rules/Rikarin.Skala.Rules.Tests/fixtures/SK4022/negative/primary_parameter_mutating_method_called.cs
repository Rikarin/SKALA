// ⚠ #412. `readonly struct Tally` compiles, and `Bump` then mutates a defensive copy of `counter`:
// `Probe.Run()` is 1 as written and 0 after the fix. A non-readonly method called on a struct-typed
// capture is a write the compiler cannot see, so the rule declines.
public struct Counter {
    public int Count;

    public void Increment() => Count++;
}

struct Tally(Counter counter) {
    public void Bump() => counter.Increment();

    public int Count => counter.Count;
}

public static class Probe {
    public static int Run() {
        var tally = new Tally(new Counter());
        tally.Bump();
        return tally.Count;
    }
}
