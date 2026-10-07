// ⚠ #425: over `Array` the element is an `object` and `long` unboxes it, which throws for a boxed enum;
// over `T[]` the element is the enum and `long` converts it. Measured for #412's audit:
// `InvalidCastException` before the fix and `0`, `1` after it.
using System;

public enum Color {
    Red,
    Green
}

public static class Probe {
    public static long Run() {
        long total = 0;
        foreach (long value in Enum.GetValues(typeof(Color))) {
            total += value;
        }

        return total;
    }
}
