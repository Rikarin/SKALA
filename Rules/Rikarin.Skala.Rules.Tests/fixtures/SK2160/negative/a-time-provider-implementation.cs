using System;

// ⚠ Somebody has to read the real clock, and `TimeProvider` is where the framework says it happens.
// Reporting the read inside a provider would be reporting the repair the rule asks for.
public sealed class SystemClock : TimeProvider {
    public override DateTimeOffset GetUtcNow() => DateTime.UtcNow;
}
