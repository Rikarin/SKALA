namespace Fixtures.SK2240;

public sealed record Point(int X, int Y);

public class ReceiverIsAField {
    Point origin = new(0, 0);

    // Only a local or a parameter is known not to change between the two forms, so a field read is
    // declined.
    public Point Move(int x, int y) => origin with { X = x, Y = y };
}
