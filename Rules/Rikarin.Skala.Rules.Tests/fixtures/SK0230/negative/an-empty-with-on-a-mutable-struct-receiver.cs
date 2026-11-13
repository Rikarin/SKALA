public struct Counter {
    public int Value;

    public void Increment() => Value++;
}

public sealed class Tally {
    Counter counter;

    // `with` on a plain struct (C# 10). As a receiver the copy is what is mutated; without it the
    // field would be.
    public int Preview() {
        (counter with { }).Increment();
        return counter.Value;
    }
}
