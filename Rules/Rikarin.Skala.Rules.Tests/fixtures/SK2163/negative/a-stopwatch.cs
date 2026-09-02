using System;
using System.Diagnostics;

// The repaired shape, which is also what this rule's fix produces.
public sealed class Work {
    public TimeSpan Run() {
        var start = Stopwatch.StartNew();
        Console.WriteLine("working");
        return start.Elapsed;
    }
}
