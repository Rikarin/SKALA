using System;

public sealed class Flag<T> where T : Enum {
    public Flag(T value) {
        Value = value;
    }

    public T Value { get; }
}
