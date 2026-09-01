namespace Contoso.Design;

// No declared constructor at all: the implicit one is public and the type is closed to nothing.
public sealed class Order {
    public int Id { get; init; }
}

// A public constructor beside the private one keeps the type reachable.
public sealed class Money {
    public Money(decimal amount) => Amount = amount;

    private Money() { }

    public decimal Amount { get; }
}

// A positional record has a public primary constructor, so a private one closes nothing.
public sealed record Token(string Value) {
    private Token() : this(string.Empty) { }
}

// A struct always has an implicit parameterless constructor.
public struct Point {
    private Point(int x) => X = x;

    public int X { get; }
}
