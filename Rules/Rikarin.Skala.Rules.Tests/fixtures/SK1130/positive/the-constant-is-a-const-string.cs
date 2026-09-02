using System;

// A constant pattern accepts a `const string`, so the argument is re-spelled rather than inlined.
public static class Transforms {
    const string World = "world";

    public static bool IsWorld(ReadOnlySpan<char> name) => name.SequenceEqual(World);
}
