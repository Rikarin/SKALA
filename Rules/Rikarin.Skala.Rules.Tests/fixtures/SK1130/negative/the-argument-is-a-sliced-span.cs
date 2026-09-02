using System;

// Only the no-argument `AsSpan()` unwraps. `AsSpan(1, 3)` is a slice, and its value is not the literal.
public static class Names {
    public static bool IsOrl(ReadOnlySpan<char> name) => name.SequenceEqual("world".AsSpan(1, 3));
}
