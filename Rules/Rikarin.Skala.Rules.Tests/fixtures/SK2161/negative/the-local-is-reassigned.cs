using System;

// The local is written twice, so the initializer does not decide what the value is at the conversion.
public sealed class Schedule {
    public DateTime Starts(bool other) {
        var built = new DateTime(2026, 1, 2);
        if (other) {
            built = DateTime.UtcNow;
        }

        return built.ToUniversalTime();
    }
}
