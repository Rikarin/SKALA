namespace Fixtures.SK2240;

public struct Size {
    public int Width;
    public int Height;
}

public static class PlainStructWith {
    // `with` works on a non-record struct too, and there is no primary constructor to compare against.
    public static Size Resize(Size size, int width, int height) =>
        size with { Width = width, Height = height };
}
