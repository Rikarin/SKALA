using System;

// The static spelling puts an arbitrary expression where the receiver would be. `a ? b : c is "x"`
// does not mean `(a ? b : c) is "x"`, so the span has to be primary before it may move.
public static class Transforms {
    public static bool IsWorld(ReadOnlySpan<char> left, ReadOnlySpan<char> right, bool pick) =>
        MemoryExtensions.SequenceEqual(pick ? left : right, "world");
}
