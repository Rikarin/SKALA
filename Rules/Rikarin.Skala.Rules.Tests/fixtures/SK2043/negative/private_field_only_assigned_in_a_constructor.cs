sealed class Frozen {
    int id;

    public Frozen(int id) => this.id = id;

    public override bool Equals(object? other) => other is Frozen frozen && frozen.id == id;

    public override int GetHashCode() => id;
}
