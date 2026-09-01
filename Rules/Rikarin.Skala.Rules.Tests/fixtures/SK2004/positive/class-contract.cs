using System;

class Key : IEquatable<Key> {
    public int Id;
    public bool Equals(Key? other) => other?.Id == Id;
}
