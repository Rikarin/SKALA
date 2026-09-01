using System;

public sealed class Paths {
    // ⚠ `IndexOf(char, StringComparison)` exists and `IndexOf(char, int, StringComparison)` does not,
    // so appending the offset would write a call that does not bind.
    public static bool HasSeparator(string path, int start) =>
        path.Substring(start).IndexOf('/', StringComparison.Ordinal) >= 0;
}
