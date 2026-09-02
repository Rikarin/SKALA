using System;

// An offset that was written down is the repair. Only the single-argument constructor invents one.
public sealed class Schedule {
    public DateTimeOffset Starts() => new DateTimeOffset(new DateTime(2026, 1, 2), TimeSpan.Zero);
}
