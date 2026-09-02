namespace Fixtures.SK2240;

public sealed record Point(int X, int Y);

public static class EmptyInitializerIsSk0230 {
    // ⚠ The disjointness against SK0230, asserted rather than argued: an initializer that assigns
    // nothing is SK0230's subject. This rule requires an assignment for every positional parameter of
    // a record that has at least one, so the two can never report the same expression.
    public static Point Copy(Point point) => point with { };
}
