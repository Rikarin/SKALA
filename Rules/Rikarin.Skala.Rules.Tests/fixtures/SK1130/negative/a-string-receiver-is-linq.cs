using System;
using System.Linq;

// ⚠ Measured, not assumed: on a `string` receiver the call binds to `Enumerable.SequenceEqual`, and
// a null receiver throws `ArgumentNullException` where `s is "abc"` quietly returns `false`. The two
// spellings are different programs, so this one has no rewrite.
public static class Names {
    public static bool IsWorld(string name) => name.SequenceEqual("world");
}
