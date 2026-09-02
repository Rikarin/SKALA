using System;

// ⚠ A static method that reads its argument is not publication, and reporting it would bury the
// rule: `this` appears as an argument far more often than it escapes. The gate is the call's
// *receiver* — `Console` binds to a type rather than to static state, and `Validate` has no receiver
// at all — so neither call keeps the reference past its own frame.
public sealed class Trace {
    public Trace() {
        Console.WriteLine(this);
        Validate(this);
    }

    public override string ToString() => "Trace";

    static void Validate(Trace trace) => _ = trace.ToString();
}
