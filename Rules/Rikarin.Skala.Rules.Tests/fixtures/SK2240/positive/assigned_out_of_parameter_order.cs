namespace Fixtures.SK2240;

public sealed record Pair(int First, int Second);

public static class AssignedOutOfParameterOrder {
    // The fix has to emit the arguments in *parameter* order, not initializer order.
    public static Pair Rebuild(Pair pair, int first, int second) =>
        pair with { Second = second, First = first };
}
