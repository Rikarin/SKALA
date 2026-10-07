using System;
using System.Threading;

// `delegate() { … }` has an explicit — empty — parameter list, so it is `() => { … }` exactly.
// `Thread` overloads on `ThreadStart` and `ParameterizedThreadStart`; both spellings pick the first.
public static class Workers {
    public static Thread Start() {
        var thread = new Thread(delegate() { Console.WriteLine("work"); });
        thread.Start();
        return thread;
    }
}
