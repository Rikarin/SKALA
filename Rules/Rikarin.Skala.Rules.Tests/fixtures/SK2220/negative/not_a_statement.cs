// The call is the body of an expression-bodied member and of a lambda, not a statement of its own.
// Both are genuinely dead in the same way, and both are declined: the fix deletes a statement, and
// there is no deletion here that leaves the rest of the declaration standing.
using System;
using System.Diagnostics;

class Log {
    [Conditional("DEBUG")]
    public static void Trace(string message) => Console.WriteLine(message);
}

class C {
#if !DEBUG
    void Bodied() => Log.Trace("an expression-bodied member");

    Action Lambda() => () => Log.Trace("a lambda body");
#endif
}
