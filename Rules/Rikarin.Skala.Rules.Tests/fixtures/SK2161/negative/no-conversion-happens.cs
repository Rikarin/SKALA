using System;

// The value is only compared with and formatted from others of the same unstated zone. That is a domain
// which does not model time zones — a decision rather than a defect — and the rule reports the escape,
// never the value.
public sealed class Schedule {
    public bool Before(DateTime other) => new DateTime(2026, 1, 2) < other;

    public int Year() => new DateTime(2026, 1, 2).Year;

    public DateTime Shifted() => new DateTime(2026, 1, 2).AddDays(3);
}
