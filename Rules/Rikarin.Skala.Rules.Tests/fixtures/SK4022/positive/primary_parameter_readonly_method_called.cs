// A `readonly` member called on a struct-typed capture runs on the capture itself before and after
// the fix: `readonly` is what lets the compiler skip the defensive copy (#412).
public struct Counter {
    public int Count;

    public void Increment() => Count++;

    public readonly int Doubled() => Count * 2;
}

struct Report(Counter counter) {
    public int Doubled => counter.Doubled();
}

public static class Probe {
    public static int Run() {
        var counter = new Counter();
        counter.Increment();
        return new Report(counter).Doubled;
    }
}
