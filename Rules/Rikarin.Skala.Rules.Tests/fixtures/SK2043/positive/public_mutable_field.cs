sealed class Entry {
    public int Key;

    public override bool Equals(object? other) => other is Entry entry && entry.Key == Key;

    public override int GetHashCode() => Key;
}
