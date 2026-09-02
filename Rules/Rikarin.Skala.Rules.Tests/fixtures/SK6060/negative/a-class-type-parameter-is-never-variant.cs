public sealed class Box<T> {
    public T? Value { get; init; }

    public T Create() => default!;
}

public delegate T Producer<T>();
