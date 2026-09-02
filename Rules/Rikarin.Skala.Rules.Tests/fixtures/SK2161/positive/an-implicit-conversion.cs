using System;

public sealed class Schedule {
    // ⚠ No `new` and no cast: the compiler inserts the conversion, and with it the machine's offset.
    public DateTimeOffset Starts() {
        DateTimeOffset when = new DateTime(2026, 1, 2);
        return when;
    }
}
