sealed class Counter {
    public int Value { get; init; }

    public override int GetHashCode() => Value;
}
