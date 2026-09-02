using System;

// The second reader would keep the old type and stop compiling once the declaration became a
// `Stopwatch`, so the rule declines rather than offering a fix that breaks the build.
public sealed class Work {
    public TimeSpan Run() {
        var start = DateTime.UtcNow;
        Console.WriteLine(start);
        return DateTime.UtcNow - start;
    }
}
