namespace Fixtures.SK2240;

public sealed record Point(int X, int Y);

public static class EmptyInitializerIsSk0230 {
    // ⚠ An initializer that assigns nothing is outside this rule, asserted rather than argued: it
    // requires an assignment for every positional parameter of a record that has at least one. (It is
    // outside SK0230 too since #636 — an empty `with` is a copy, not a no-op.)
    public static Point Copy(Point point) => point with { };
}
