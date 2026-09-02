namespace Fixtures.SK2240;

public sealed record Point(int X, int Y);

public static class CarriesAMemberAcross {
    // ⚠ The fix-loop guard. Every member is assigned, so the bare shape matches — but `X = point.X`
    // would fix to `new Point(point.X, y)`, which is exactly SK1071's input, and SK1071 would fix that
    // straight back to this. Reporting it would make `skala fix` oscillate between two rules forever.
    public static Point Slide(Point point, int y) => point with { X = point.X, Y = y };
}
