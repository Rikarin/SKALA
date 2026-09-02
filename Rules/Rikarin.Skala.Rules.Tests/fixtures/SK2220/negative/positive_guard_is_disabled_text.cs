// ⚠ This fixture documents what the rule CANNOT see, and it is the reason the redundant shape is
// not this rule. With DEBUG undefined the region below is disabled text: it holds no invocation
// node, so no analysis of any kind runs inside it. A "redundant guard" rule written against this
// shape would pass its own fixture by never running, which is the failure a negative fixture cannot
// distinguish from correctness — so it is recorded here as an absence rather than shipped as a rule.
using System;
using System.Diagnostics;

class Log {
    [Conditional("DEBUG")]
    public static void Trace(string message) => Console.WriteLine(message);
}

class C {
    void M() {
#if DEBUG
        Log.Trace("invisible to every analyzer in this compilation");
#endif
    }
}
