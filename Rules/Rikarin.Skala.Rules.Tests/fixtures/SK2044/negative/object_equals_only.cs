sealed class Simple {
    public int Id { get; init; }

    public override bool Equals(object? other) => other is Simple simple && simple.Id == Id;

    public override int GetHashCode() => Id;
}
