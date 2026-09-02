using System;

// A pattern needs a compile-time constant; a parameter is not one.
public static class Names {
    public static bool Matches(ReadOnlySpan<char> name, string other) => name.SequenceEqual(other);
}
