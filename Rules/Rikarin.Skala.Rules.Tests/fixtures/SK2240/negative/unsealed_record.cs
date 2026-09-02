namespace Fixtures.SK2240;

public record Open(int X, int Y);

public static class UnsealedRecord {
    // ⚠ `x with { … }` returns x's *runtime* type through the virtual clone; `new Open(…)` returns
    // exactly `Open`. Holding a derived instance those are two different objects.
    public static Open Move(Open value, int x, int y) => value with { X = x, Y = y };
}
