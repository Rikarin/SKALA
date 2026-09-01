sealed class Entry {
    readonly int key;

    public Entry(int key) => this.key = key;

    public override bool Equals(object? other) => other is Entry entry && entry.key == key;

    public override int GetHashCode() => key;
}
