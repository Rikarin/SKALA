public sealed record Point(int X, int Y);

public static class Shifted {
    public static Point Right(Point source) => source with { X = source.X + 1 };
}
