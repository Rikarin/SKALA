public record struct Point(int X, int Y);

public sealed class Mover {
    public Point Right(Point point) {
        var moved = new Point(point.X + 1, point.Y);
        return moved;
    }
}
