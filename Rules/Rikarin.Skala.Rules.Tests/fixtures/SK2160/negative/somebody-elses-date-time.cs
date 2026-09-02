using System;

// A source type of the same name is never matched: every type test requires the symbol to come from
// metadata.
public struct DateTime {
    public static DateTime UtcNow => default;
}

public sealed class Reader {
    public DateTime Read() => DateTime.UtcNow;
}
