sealed class Key {
    public string Name { get; init; } = "";

    public override bool Equals(object? other) => other is Key key && key.Name == Name;

    public override int GetHashCode() => base.GetHashCode();
}
