using System;

sealed class Tag : IEquatable<Tag> {
    public int Id { get; init; }

    public bool Equals(Tag? other) => base.Equals(other);

    public override bool Equals(object? other) => Equals(other as Tag);

    public override int GetHashCode() => Id;
}
