sealed class Marker {
    public int Id { get; init; }

    public override bool Equals(object? other) => other is Marker;

    public override int GetHashCode() => Id;
}
