// Each of these already works on a copy of the capture, so `readonly` changes none of them (#412):
// an `in` extension, a deconstruction, an auto-property getter, and a mutating call on a local copy.
public struct Counter {
    public int Count;

    public int Seen { get; }

    public void Increment() => Count++;

    public void Deconstruct(out int count, out int seen) {
        Count++;
        count = Count;
        seen = Seen;
    }
}

public static class CounterExtensions {
    public static int Peek(this in Counter counter) => counter.Count;
}

struct Tally(Counter counter) {
    public int Read() {
        var (count, seen) = counter;
        var copy = counter;
        copy.Increment();
        return counter.Peek() + counter.Seen + count + seen + copy.Count;
    }
}

public static class Probe {
    public static int Run() => new Tally(new Counter()).Read();
}
