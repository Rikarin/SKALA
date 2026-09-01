using System;

public sealed class Paths {
    public static bool Mentions(string text, string needle) =>
        text.Substring(4).IndexOf(needle, StringComparison.Ordinal) != -1;
}
