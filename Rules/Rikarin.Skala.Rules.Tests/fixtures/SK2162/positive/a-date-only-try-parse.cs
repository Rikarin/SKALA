using System;

public sealed class Import {
    public bool Read(string text) => DateOnly.TryParse(text, out _);
}
