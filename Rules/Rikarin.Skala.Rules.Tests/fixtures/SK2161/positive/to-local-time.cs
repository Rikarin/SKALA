using System;

public sealed class Schedule {
    // The same `Kind`, the opposite assumption: `ToLocalTime` treats it as UTC and *adds* the offset.
    public DateTime StartsAtLocal() => new DateTime(2026, 3, 29, 2, 30, 0).ToLocalTime();
}
