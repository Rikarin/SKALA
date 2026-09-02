using System;

public sealed class Audit {
    public DateTimeOffset Stamp() => DateTimeOffset.UtcNow;
}
