using System;

// The rule is about the types whose textual form is a date or a time. `int.TryParse` is culture
// -sensitive too and is `CA1305`'s business, not this rule's.
public sealed class Import {
    public bool Number(string text) => int.TryParse(text, out _);

    public bool Enumeration(string text) => Enum.TryParse<DayOfWeek>(text, out _);

    public bool Boolean(string text) => bool.TryParse(text, out _);
}
