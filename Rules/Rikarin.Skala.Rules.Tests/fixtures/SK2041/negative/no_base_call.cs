sealed class Coin {
    public int Value { get; init; }

    public override bool Equals(object? other) => other is Coin coin && coin.Value == Value;

    public override int GetHashCode() => Value;
}
