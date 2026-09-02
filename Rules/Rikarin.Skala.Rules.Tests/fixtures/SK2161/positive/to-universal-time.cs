using System;

public sealed class Schedule {
    // `Kind` is `Unspecified`, so `ToUniversalTime` treats the value as *local* and subtracts the
    // running machine's offset from a value that never said it was local.
    public DateTime StartsAtUtc() => new DateTime(2026, 3, 29, 2, 30, 0).ToUniversalTime();
}
