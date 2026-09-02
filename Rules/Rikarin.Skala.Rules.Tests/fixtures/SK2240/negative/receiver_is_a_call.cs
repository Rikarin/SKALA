namespace Fixtures.SK2240;

public sealed record Point(int X, int Y);

public static class ReceiverIsACall {
    static Point Current() => new(0, 0);

    // ⚠ `new Point(x, y)` would drop the call entirely — an evaluation the original performed.
    public static Point Move(int x, int y) => Current() with { X = x, Y = y };
}
