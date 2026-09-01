public sealed class Order {
    public int Id { get; init; }
}

public readonly record struct Money(decimal Amount);

public sealed record Customer(string Name);
