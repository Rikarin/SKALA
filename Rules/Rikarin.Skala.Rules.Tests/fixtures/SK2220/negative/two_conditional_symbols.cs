// ⚠ The guard that would break the rule if `[Conditional]` were read as a single attribute.
// `Either` carries two: the call survives when EITHER symbol is defined, so proving DEBUG absent
// proves nothing at all about whether this call is deleted. Reporting it would call live code dead.
using System;
using System.Diagnostics;

class Log {
    [Conditional("DEBUG")]
    [Conditional("VERBOSE")]
    public static void Either(string message) => Console.WriteLine(message);
}

class C {
    void M() {
#if !DEBUG
        Log.Either("VERBOSE may still be defined by the build that compiles this");
#endif
    }
}
