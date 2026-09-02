// A compound condition is declined. `#if !DEBUG && !VERBOSE` being taken does prove DEBUG absent,
// but the shape generalises badly — `#if !DEBUG || OTHER` proves nothing — and the rule reads two
// directive shapes and no others rather than growing a preprocessor evaluator.
using System;
using System.Diagnostics;

class Log {
    [Conditional("DEBUG")]
    public static void Trace(string message) => Console.WriteLine(message);
}

class C {
    void M() {
#if !DEBUG && !VERBOSE
        Log.Trace("declined: the condition is not one of the two shapes read");
#endif
    }
}
