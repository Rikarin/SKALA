using System;

public sealed class Paths {
    // The rewrite would turn a `bool` atom into a relational expression, which is a different rule.
    public static bool Mentions(string text, string needle) =>
        text.Substring(4).Contains(needle, StringComparison.Ordinal);
}
