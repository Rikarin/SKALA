namespace Fixtures.SK2240;

public sealed record Point(int X, int Y);

public static class SomeMembersOnly {
    // `Y` is carried across by the copy constructor, which is what a `with` expression is for.
    public static Point Slide(Point point, int x) => point with { X = x };
}
