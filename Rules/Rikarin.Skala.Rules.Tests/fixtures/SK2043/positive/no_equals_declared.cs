sealed class Counter {
    public int Value { get; set; }

    public override int GetHashCode() => Value;
}
