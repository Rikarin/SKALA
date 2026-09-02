using System;

public sealed class Work {
    public TimeSpan Run() {
        var start = DateTimeOffset.UtcNow;
        Console.WriteLine("working");
        return DateTimeOffset.UtcNow - start;
    }
}
