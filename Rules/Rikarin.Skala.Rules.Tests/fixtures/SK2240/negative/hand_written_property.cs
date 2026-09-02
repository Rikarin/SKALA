namespace Fixtures.SK2240;

public sealed record Custom(int X, int Y) {
    // ⚠ Written out by hand, so it is *not* the auto-property the record synthesized: `value.X`
    // calls this accessor and the copy constructor does not, moving the backing field across instead.
    public int X { get; init; } = X < 0 ? 0 : X;
}

public static class HandWrittenProperty {
    public static Custom Move(Custom value, int x, int y) => value with { X = x, Y = y };
}
