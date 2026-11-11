// fixture-option: TargetFramework = net10.0
// ⚠ A net10.0 fact, pinned to net10.0 references: through .NET 10 `IndexOf(char, StringComparison)`
// exists and `IndexOf(char, int, StringComparison)` does not, so appending the offset would write a
// call that does not bind. .NET 11 added the overload, and the same file is the positive
// `a-char-search-with-a-comparison-on-net11`.
using System;

public sealed class Paths {
    public static bool HasSeparator(string path, int start) =>
        path.Substring(start).IndexOf('/', StringComparison.Ordinal) >= 0;
}
