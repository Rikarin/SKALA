public sealed class Money {
    public static bool operator ==(Money? left, Money? right) => ReferenceEquals(left, right);

    public static bool operator !=(Money? left, Money? right) => !(left == right);

    public override bool Equals(object? other) => other is Money;

    public override int GetHashCode() => 0;
}

public sealed class Wallet {
    public static bool Has(Money? money) => money != null;
}
