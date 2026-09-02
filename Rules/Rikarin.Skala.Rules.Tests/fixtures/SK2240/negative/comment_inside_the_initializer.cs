namespace Fixtures.SK2240;

public sealed record Point(int X, int Y);

public static class CommentInsideTheInitializer {
    // The fix replaces the whole expression, so a comment inside it would be deleted by a fix nobody
    // can review.
    public static Point Move(Point point, int x, int y) =>
        point with { X = x, /* clamped upstream */ Y = y };
}
