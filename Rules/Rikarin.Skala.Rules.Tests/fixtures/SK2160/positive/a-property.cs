using System;

public sealed class Session {
    // `DateTime.Now` additionally binds the value to the machine's time zone, so the same session
    // reads as a different age on two servers.
    public DateTime StartedAt { get; } = DateTime.Now;
}
