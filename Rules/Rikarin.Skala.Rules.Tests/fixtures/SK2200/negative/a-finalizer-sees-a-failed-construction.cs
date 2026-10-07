// ⚠ #431: a constructor that throws before the overwrite still leaves an instance to finalize, and the
// finalizer reads the initialized value. Measured `5` before the fix and `0` after, through
// `GC.Collect` and `WaitForPendingFinalizers`; not a `Probe`, because when a finalizer runs is the
// collector's business.
public sealed class Handle {
    int code = 5;

    public Handle(int divisor) {
        var quotient = 10 / divisor;
        code = quotient;
    }

    ~Handle() {
        System.Console.WriteLine(code);
    }
}
