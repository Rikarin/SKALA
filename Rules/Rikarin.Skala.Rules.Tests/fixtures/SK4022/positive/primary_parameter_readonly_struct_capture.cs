// Every member of a `readonly struct` is `readonly`, so calling any of them on the capture copies
// nothing either way (#412). `TimeSpan` is one from metadata.
using System;

struct Deadline(TimeSpan span) {
    public double Minutes() => span.TotalMinutes + span.Add(span).TotalMinutes;
}

public static class Probe {
    public static int Run() => (int)new Deadline(TimeSpan.FromMinutes(2)).Minutes();
}
