// ⚠ #425: a lambda written inside `checked { }` is checked too, so `(uint)value` throws for a negative
// value, where `>>>` never throws. Measured for #412's audit: `OverflowException` before the fix and
// `2147483644` after it.
using System;

public static class Probe {
    public static string Run() {
        checked {
            Func<int, int> shift = value => {
                return (int)((uint)value >> 1);
            };
            try {
                return shift(-8).ToString();
            } catch (OverflowException) {
                return "overflow";
            }
        }
    }
}
