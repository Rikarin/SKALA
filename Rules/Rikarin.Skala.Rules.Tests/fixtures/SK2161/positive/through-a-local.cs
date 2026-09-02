using System;

public sealed class Schedule {
    public DateTime Starts() {
        var built = new DateTime(2026, 1, 2, 3, 4, 5);
        return built.ToUniversalTime();
    }
}
