// ⚠ The fixture harness parses with no preprocessor symbols defined, so `#if !DEBUG` is the ACTIVE
// branch and the call below is a real invocation node the analyzer can see. Written the other way
// round — `#if DEBUG` — the statement would be disabled text, the rule would not run on it, and the
// fixture would be testing the preprocessor instead of the rule.
using System;
using System.Diagnostics;

class Log {
    [Conditional("DEBUG")]
    public static void Trace(string message) => Console.WriteLine(message);
}

class C {
    void M() {
#if !DEBUG
        Log.Trace("this statement runs in no build at all");
#endif
    }
}
