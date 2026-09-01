using System;

sealed class Item {
    public int Id { get; init; }

    public string Name { get; init; } = "";

    public override bool Equals(object? other) => other is Item item && Matches(item);

    public override int GetHashCode() => HashCode.Combine(Id, Name);

    bool Matches(Item other) => other.Id == Id;
}
