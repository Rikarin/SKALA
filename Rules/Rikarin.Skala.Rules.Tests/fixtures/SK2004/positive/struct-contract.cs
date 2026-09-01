using System;

struct Key : IEquatable<Key> {
    public int Id;
    public bool Equals(Key other) => other.Id == Id;
}
