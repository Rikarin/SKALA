sealed class Money {
    public decimal Amount { get; init; }

    public static bool operator ==(Money? left, Money? right) => left?.Amount == right?.Amount;

    public static bool operator !=(Money? left, Money? right) => !(left == right);
}
