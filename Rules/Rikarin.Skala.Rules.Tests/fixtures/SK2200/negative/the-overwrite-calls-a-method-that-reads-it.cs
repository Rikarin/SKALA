// ⚠ #431: found while fixing it — the value written by the overwrite is computed by a method that reads
// the field first, so the initialized value is what it doubles. Only the statements *before* the write
// used to be checked. Measured `6` before the fix and `0` after.
public sealed class Doubler {
    int value = 3;

    public Doubler() {
        value = Twice();
    }

    int Twice() => value * 2;

    public int Value => value;
}

public static class Probe {
    public static int Run() => new Doubler().Value;
}
