using System;

// `UtcNow` is `Utc` and `Now` is `Local`; neither is a constructor call, so neither is a source this
// rule treats as unspecified.
public sealed class Schedule {
    public DateTime FromUtc() => DateTime.UtcNow.ToLocalTime();

    public DateTime FromLocal() => DateTime.Now.ToUniversalTime();
}
