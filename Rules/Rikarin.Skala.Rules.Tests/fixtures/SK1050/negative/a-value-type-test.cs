// A boxed value type is never null, so `is not null` is not the question `is int` asks.
public sealed class Inspector {
    public bool IsNumber(object value) => value is int;

    public bool IsNumber(string text) => text.Length is int;
}
