public sealed record Point(int X, int Y);

public static class Origins {
    public static bool AtOrigin(object value) => value is Point { X: 0, Y: 0 } _;
}
