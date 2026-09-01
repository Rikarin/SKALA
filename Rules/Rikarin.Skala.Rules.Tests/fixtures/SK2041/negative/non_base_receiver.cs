sealed class Pair {
    public int Value { get; init; }

    public override bool Equals(object? other) => other is Pair pair && pair.Value.Equals(Value);

    public override int GetHashCode() => Value.GetHashCode();
}
