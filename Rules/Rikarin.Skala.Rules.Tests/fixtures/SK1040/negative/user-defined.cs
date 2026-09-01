namespace Custom;

// A user type may be called `Nullable<T>`. It has no short form, so the name alone is never
// enough — the symbol is bound and compared against System.Nullable<T> first.
public sealed class Nullable<T> {
    public T? Value { get; init; }
}

public sealed class Holder {
    public Nullable<int> Boxed { get; } = new();
}
