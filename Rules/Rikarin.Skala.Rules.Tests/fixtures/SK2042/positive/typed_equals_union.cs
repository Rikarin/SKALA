using System;

sealed class Key : IEquatable<Key> {
    public int Id { get; init; }

    public string Name { get; init; } = "";

    public int Version { get; init; }

    public bool Equals(Key? other) => other is not null && other.Id == Id && other.Name == Name;

    public override bool Equals(object? other) => Equals(other as Key);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Version);
}
