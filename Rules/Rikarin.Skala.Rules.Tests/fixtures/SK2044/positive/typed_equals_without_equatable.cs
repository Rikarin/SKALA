sealed class Key {
    public int Id { get; init; }

    public bool Equals(Key? other) => other is not null && other.Id == Id;

    public override bool Equals(object? other) => other is Key key && Equals(key);

    public override int GetHashCode() => Id;
}
