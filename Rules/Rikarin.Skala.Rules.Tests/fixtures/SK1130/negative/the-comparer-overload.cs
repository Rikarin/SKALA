using System;
using System.Collections.Generic;

// The comparer overload has no pattern spelling — a constant pattern cannot carry an
// `IEqualityComparer<T>`. In the static spelling it passes three arguments, so the operand split
// refuses it: the split is what identifies which argument is the span, and it only knows that for
// the exact arities.
public static class Names {
    public static bool IsWorld(ReadOnlySpan<char> name, IEqualityComparer<char> comparer) =>
        MemoryExtensions.SequenceEqual(name, "world", comparer);
}
