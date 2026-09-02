using System;

// ⚠ The boundary of this rule, written down as a fixture. `CA1305` ships in the SDK and reports every
// one of these; ADR-008 hosts rather than rebuilds, so Skala must stay silent here or the two tools
// would both report the same line.
public sealed class Import {
    public DateTime Parse(string text) => DateTime.Parse(text);

    public DateTimeOffset Offset(string text) => DateTimeOffset.Parse(text);

    public string Format(DateTime value) => value.ToString();

    public string Formatted(DateTime value) => value.ToString("yyyy-MM-dd");
}
