using System;

class Base {
    public override bool Equals(object? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => 0;
}

class Key : Base, IEquatable<Key> {
    public bool Equals(Key? other) => base.Equals(other);
}
