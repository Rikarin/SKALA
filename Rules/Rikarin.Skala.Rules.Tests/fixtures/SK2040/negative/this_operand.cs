sealed class Money {
    public decimal Amount { get; init; }

    public override bool Equals(object? other) => other is Money money && money.Amount == Amount;

    public override int GetHashCode() => Amount.GetHashCode();

    public bool IsSameInstance(Money other) => this == other;
}
