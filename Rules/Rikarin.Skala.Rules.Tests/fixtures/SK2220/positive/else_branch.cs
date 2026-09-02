// The `#else` of a plain `#if X`: reaching it proves X is not defined, which deletes the call.
using System;
using System.Diagnostics;

class Log {
    [Conditional("VERBOSE")]
    public static void Say(string message) => Console.WriteLine(message);
}

class C {
    void M() {
#if VERBOSE
        Console.WriteLine("verbose");
#else
        Log.Say("the else branch proves VERBOSE is undefined, so this call is deleted");
#endif
    }
}
