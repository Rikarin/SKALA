// ⚠ #425: the handler renders an `IFormattable` through `ToString(null, null)`, not through
// `ToString()`, and a type may answer the two differently. Measured for #412's audit: "plain" before the
// fix and "formatted" after it.
using System;

public readonly struct Money : IFormattable {
    public override string ToString() => "plain";

    public string ToString(string? format, IFormatProvider? provider) => "formatted";
}

public static class Probe {
    public static string Run() {
        Money money = default;
        return $"c {money.ToString()}";
    }
}
