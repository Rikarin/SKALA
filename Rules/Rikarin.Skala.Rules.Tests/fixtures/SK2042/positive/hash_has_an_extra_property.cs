using System;

sealed class Order {
    public int Id { get; init; }

    public string Customer { get; init; } = "";

    public override bool Equals(object? other) => other is Order order && order.Id == Id;

    public override int GetHashCode() => HashCode.Combine(Id, Customer);
}
