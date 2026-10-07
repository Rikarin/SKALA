// The names travel with a type argument: `Box<(int Width, int Height)>.Value` has type
// `(int Width, int Height)`.
public sealed class Box<T> {
    public T Value = default!;
}

public static class Sizes {
    public static int Width(Box<(int Width, int Height)> box) => box.Value.Item1;
}
