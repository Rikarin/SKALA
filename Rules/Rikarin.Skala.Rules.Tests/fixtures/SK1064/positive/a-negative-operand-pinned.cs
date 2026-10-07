// Outside a checked context the round trip reinterprets the bits both ways, so a negative operand
// shifts in zeros exactly as `>>>` does — and a lambda in an `unchecked` block inside a checked one is
// unchecked. Pinned by Probe (#425).
using System;

public static class Probe {
    public static string Run() {
        var value = -8;
        long wide = -8;
        var narrow = (int)((uint)value >> 1);
        var long2 = (long)((ulong)wide >> 2);
        var text = narrow + "/" + long2;
        checked {
            unchecked {
                Func<int, int> shift = operand => {
                    return (int)((uint)operand >> 3);
                };
                text += "/" + shift(-8);
            }
        }

        return text;
    }
}
