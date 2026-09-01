sealed class Money {
    public decimal Amount { get; init; }

    public static bool operator ==(Money? left, Money? right) => Equals(left, right);

    public static bool operator !=(Money? left, Money? right) => !(left == right);

    public override bool Equals(object? other) => other is Money money && money.Amount == Amount;

    public override int GetHashCode() => Amount.GetHashCode();
}

class C {
    bool Same(Money left, Money right) => left == right;
}
