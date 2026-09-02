// The guard and the attribute name different symbols, so the guard says nothing about the deletion.
using System;
using System.Diagnostics;

class Log {
    [Conditional("DEBUG")]
    public static void Trace(string message) => Console.WriteLine(message);
}

class C {
    void M() {
#if !TRACE
        Log.Trace("guarded by TRACE, deleted by DEBUG: two independent questions");
#endif
    }
}
