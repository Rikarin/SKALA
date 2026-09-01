using System;

struct Key : IEquatable<Key> {
    public int Id;
    public bool Equals(Key other) => other.Id == Id;
    public override bool Equals(object? other) => other is Key key && Equals(key);
    public override int GetHashCode() => Id;
}
