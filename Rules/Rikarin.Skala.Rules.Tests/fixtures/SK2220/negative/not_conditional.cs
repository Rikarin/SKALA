// A `#if !DEBUG` guard over an ordinary method. The directive is doing real work here: this is how
// a release-only code path is written, and it is the commonest shape the rule must walk past.
using System;

class Log {
    public static void Trace(string message) => Console.WriteLine(message);
}

class C {
    void M() {
#if !DEBUG
        Log.Trace("release-only, and it really does run");
#endif
    }
}
