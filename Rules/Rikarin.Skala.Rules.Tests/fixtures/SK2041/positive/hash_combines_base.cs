using System;

sealed class Blend {
    public int Id { get; init; }

    public override bool Equals(object? other) => other is Blend blend && blend.Id == Id;

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Id);
}
