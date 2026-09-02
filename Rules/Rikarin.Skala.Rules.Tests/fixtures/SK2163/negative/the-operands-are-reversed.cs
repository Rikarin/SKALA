using System;

// `start - DateTime.UtcNow` is a negative duration and a different mistake. Attaching this rule's
// message and this rule's fix to it would describe code it does not describe.
public sealed class Work {
    public TimeSpan Run() {
        var start = DateTime.UtcNow;
        Console.WriteLine("working");
        return start - DateTime.UtcNow;
    }
}
