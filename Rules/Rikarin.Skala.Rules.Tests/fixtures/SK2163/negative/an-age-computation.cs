using System;

// ⚠ "How old is this order" is a legitimate question about civil time that a `Stopwatch` cannot answer
// at all. Only when the earlier value also came from this program reading the clock is the subtraction
// a measurement of elapsed time — which is why both ends are required.
public sealed class Order {
    public DateTime PlacedAt { get; init; }

    public TimeSpan Age() => DateTime.UtcNow - PlacedAt;

    public TimeSpan AgeOf(DateTime placed) => DateTime.UtcNow - placed;
}
