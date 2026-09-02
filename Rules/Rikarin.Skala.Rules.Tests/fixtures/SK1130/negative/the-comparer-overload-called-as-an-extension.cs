using System;
using System.Collections.Generic;

// ⚠ The same overload in the spelling that passes the *other* arity. Called as an extension the
// comparer call has two arguments where the static one has three, so a single arity test would have
// let one of the two through — which is why both spellings have a fixture and why the operand split
// tests each spelling's own arity.
//
// ⚠ A `Parameters.Length != 2` check on the resolved method was written for exactly this pair and
// removed as dead: the operand split already declines both, and deleting the check turned neither
// of these files red.
public static class Names {
    public static bool IsWorld(ReadOnlySpan<char> name, IEqualityComparer<char> comparer) =>
        name.SequenceEqual("world", comparer);
}
