// fixture-option: DefineConstants = RELEASE
// ⚠ The dead call sits inside `#if RELEASE`, so it is an invocation node at all only because the
// harness defines RELEASE for this fixture. Remove the directive above and the whole region is
// disabled text, the analyzer sees nothing, and this positive fixture fails — which is the point:
// it is the one fixture that measures the preprocessor configuration rather than assuming it
// (#317). Production has had `--define` all along; the fixtures had no way to say it.
using System;
using System.Diagnostics;

class Log {
    [Conditional("DEBUG")]
    public static void Trace(string message) => Console.WriteLine(message);
}

class C {
    void M() {
#if RELEASE
#if !DEBUG
        Log.Trace("this statement runs in no build at all");
#endif
#endif
    }
}
