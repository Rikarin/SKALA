using System;

// The earlier end is a literal date, so this measures how long ago a fixed moment was — not elapsed
// time, and not something a `Stopwatch` can express.
public sealed class Work {
    public TimeSpan SinceEpoch() {
        var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return DateTime.UtcNow - start;
    }
}
