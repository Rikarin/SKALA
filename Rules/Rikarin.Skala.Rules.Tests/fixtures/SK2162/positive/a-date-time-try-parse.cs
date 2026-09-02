using System;

public sealed class Import {
    // `"01/02/2026"` succeeds on every machine and means a different day on some of them.
    public DateTime? Read(string text) => DateTime.TryParse(text, out var when) ? when : null;
}
