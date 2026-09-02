public sealed record Pair(int Left, int Right);

public static class Splitting {
    // A parenthesised designation is not a discard designation, and the `_` inside it is a
    // component of the deconstruction rather than a name for the whole match.
    public static int Left(object value) => value is Pair (var left, _) ? left : 0;
}
