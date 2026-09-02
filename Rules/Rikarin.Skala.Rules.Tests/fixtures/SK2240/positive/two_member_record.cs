namespace Fixtures.SK2240;

public sealed record Point(int X, int Y);

public static class TwoMemberRecord {
    // Both positional members are assigned, so nothing of `point` survives: this is `new Point(x, y)`.
    public static Point Move(Point point, int x, int y) => point with { X = x, Y = y };
}
