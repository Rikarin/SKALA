using System;

public sealed class Import {
    public bool Read(string text) => DateTimeOffset.TryParse(text, out _);
}
