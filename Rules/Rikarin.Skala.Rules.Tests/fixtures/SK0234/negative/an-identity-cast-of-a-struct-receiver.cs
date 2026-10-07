// ⚠ #412's audit (#425's value-copy class): `((Counter)c)` is a copy, so `.Bump()` mutates a
// temporary and `c.N` stays 0; `(c).Bump()` mutates `c` and prints 1. An identity cast on a value
// is redundant only where its result is not used as a variable.
public struct Counter {
    public int N;

    public void Bump() => N++;
}

public static class Probe {
    public static int Run() {
        var c = new Counter();
        ((Counter)c).Bump();
        return c.N;
    }
}
