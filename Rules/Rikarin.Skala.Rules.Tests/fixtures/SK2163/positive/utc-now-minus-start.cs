using System;

public sealed class Work {
    public TimeSpan Run() {
        var start = DateTime.UtcNow;
        Console.WriteLine("working");
        return DateTime.UtcNow - start;
    }
}
