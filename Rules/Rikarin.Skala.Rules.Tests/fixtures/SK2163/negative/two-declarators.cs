using System;

// One type node is shared between two locals, so rewriting it to `var` would retype the other one as a
// `Stopwatch` as well.
public sealed class Work {
    public TimeSpan Run() {
        DateTime start = DateTime.UtcNow, deadline = DateTime.UtcNow.AddMinutes(1);
        Console.WriteLine(deadline);
        return DateTime.UtcNow - start;
    }
}
