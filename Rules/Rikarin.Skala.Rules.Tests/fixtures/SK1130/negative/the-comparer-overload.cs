using System;
using System.Collections.Generic;

// The three-parameter overload takes an `IEqualityComparer<T>`, and a constant pattern cannot carry
// one. Only the two-parameter method has a pattern spelling.
public static class Names {
    public static bool IsWorld(ReadOnlySpan<char> name, IEqualityComparer<char> comparer) =>
        MemoryExtensions.SequenceEqual(name, "world", comparer);
}
