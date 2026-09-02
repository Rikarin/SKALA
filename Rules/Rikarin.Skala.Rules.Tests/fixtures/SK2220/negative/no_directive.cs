// A `[Conditional]` call with no guard at all is the attribute working as designed. Reading the
// compilation's symbol list instead of the taken branch would report this one, which is why the rule
// does not read it.
using System;
using System.Diagnostics;

class Log {
    [Conditional("DEBUG")]
    public static void Trace(string message) => Console.WriteLine(message);
}

class C {
    void M() {
        Log.Trace("no guard: the attribute decides, and that is the point of it");
    }
}
