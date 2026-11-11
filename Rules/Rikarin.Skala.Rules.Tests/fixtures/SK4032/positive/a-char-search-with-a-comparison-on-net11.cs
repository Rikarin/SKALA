// ⚠ .NET 11 added `string.IndexOf(char, int, StringComparison)`, so the start index has somewhere to
// go and the copy is avoidable. The rule looks the overload up rather than assuming it; on net10.0
// references the same file is the negative `no-start-index-overload`.
using System;

public sealed class Paths {
    public static bool HasSeparator(string path, int start) =>
        path.Substring(start).IndexOf('/', StringComparison.Ordinal) >= 0;
}
