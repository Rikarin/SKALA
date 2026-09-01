using System;

sealed class Money : IEquatable<Money> {
    public decimal Amount { get; init; }

    public static bool operator ==(Money? left, Money? right) => Equals(left, right);

    public static bool operator !=(Money? left, Money? right) => !(left == right);

    public bool Equals(Money? other) => other is not null && other.Amount == Amount;

    public override bool Equals(object? other) => Equals(other as Money);

    public override int GetHashCode() => Amount.GetHashCode();
}
