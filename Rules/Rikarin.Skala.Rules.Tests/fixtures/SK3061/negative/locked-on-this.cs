public sealed class Counter {
    int value;

    public void Increment() {
        // ⚠ `CA2002` — *do not lock on objects with weak identity* — owns this, and the measurement
        // that decided it was made one shape per file on a pristine `net10.0` classlib: `CA2002`
        // ships `IsEnabledByDefault=False, DefaultSeverity=Warning`, so it is silent in a default
        // build and fires on `lock (this)` the moment its severity is raised. Skala's own repository
        // raises `AnalysisMode`, so a Skala rule for this shape would double-report here first.
        lock (this) {
            value++;
        }
    }
}
