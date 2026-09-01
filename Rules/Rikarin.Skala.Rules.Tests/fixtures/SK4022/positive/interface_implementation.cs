using System;

struct KeyFixture : IEquatable<KeyFixture> {
    readonly int id;

    public KeyFixture(int value) => id = value;

    public bool Equals(KeyFixture other) => id == other.id;

    public override bool Equals(object? obj) => obj is KeyFixture other && Equals(other);

    public override int GetHashCode() => id;
}
