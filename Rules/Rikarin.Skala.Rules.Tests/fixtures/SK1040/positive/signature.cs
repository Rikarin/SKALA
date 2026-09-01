using System;

public sealed class Parser {
    public static Nullable<int> Parse(string text, Nullable<int> fallback) =>
        int.TryParse(text, out var value) ? value : fallback;
}
