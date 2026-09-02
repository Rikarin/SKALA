namespace Fixtures.SK2240;

public sealed record Tagged(int X, int Y) {
    readonly int tag = 7;

    public int Tag => tag;
}

public static class RecordWithExtraField {
    // The copy constructor carries `tag` across and the constructor call would not, so the rewrite
    // would silently drop state.
    public static Tagged Move(Tagged value, int x, int y) => value with { X = x, Y = y };
}
