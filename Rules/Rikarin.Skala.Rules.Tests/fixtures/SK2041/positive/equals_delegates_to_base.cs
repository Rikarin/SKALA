sealed class Marker {
    public int Id { get; init; }

    public override bool Equals(object? other) => base.Equals(other);

    public override int GetHashCode() => Id;
}
