using System;

// The author wrote the zone down. The finding is about a zone nobody stated, so reporting this would
// be reporting the repair.
public sealed class Schedule {
    public DateTime Utc() => new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).ToUniversalTime();

    public DateTime Local() => new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();

    public DateTime Written() =>
        new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified).ToUniversalTime();
}
