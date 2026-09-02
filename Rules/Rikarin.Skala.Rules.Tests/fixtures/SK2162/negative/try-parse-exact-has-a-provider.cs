using System;
using System.Globalization;

// Every `TryParseExact` overload has the parameter, so the rule never reaches one. ⚠ An explicitly
// written `null` provider is not reported either: with a custom format string such as "yyyy-MM-dd"
// there is no culture-sensitive token, and reporting the class would report the safe majority of it.
public sealed class Import {
    public bool Exact(string text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", null, DateTimeStyles.None, out _);

    public bool Invariant(string text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
