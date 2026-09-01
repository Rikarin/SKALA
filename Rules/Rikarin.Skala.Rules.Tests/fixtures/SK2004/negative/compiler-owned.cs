using System;

class Key : IEquatable<Key> {
    public bool Equals(Key? other) => other is not null;
    public override bool Equals(object? other) => other is Key key && Equals(key);
}
