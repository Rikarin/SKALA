public delegate void Bump(ref int value);

public delegate bool Parse(string text, out int value);

public delegate int Read(in int value);

public static class Handlers {
    public static readonly Bump Increment = delegate(ref int value) { value++; };

    public static readonly Parse TryParse = delegate(string text, out int value) {
        return int.TryParse(text, out value);
    };

    public static readonly Read Next = delegate(in int value) { return value + 1; };
}
