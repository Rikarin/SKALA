// ⚠ The equality set is deliberately non-empty *before* the helper call. An Equals that reads
// nothing at all is withdrawn by a second guard, so a fixture shaped that way passes whether or
// not the helper withdrawal exists — it pins the wrong thing. Here `Id` is read first, so only
// the helper withdrawal keeps `Name` from being reported as uncompared.
using System;

sealed class Item {
    public int Id { get; init; }

    public string Name { get; init; } = "";

    public override bool Equals(object? other) => other is Item item && item.Id == Id && Matches(item);

    public override int GetHashCode() => HashCode.Combine(Id, Name);

    bool Matches(Item other) => other.Name.Length == Name.Length;
}
