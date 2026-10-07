// ⚠ #425: declared before the loop, `value` is one variable that every lambda shares; declared in the
// loop's condition, it is a new variable on every iteration and each lambda keeps its own. Measured for
// #412's audit: `4 4 4` before the fix and `1 2 3` after it.
using System;
using System.Collections.Generic;

public static class Probe {
    static int next;

    static bool Next(out int value) {
        value = ++next;
        return value <= 3;
    }

    public static string Run() {
        var readers = new List<Func<int>>();
        int value;
        while (Next(out value)) {
            readers.Add(() => value);
        }

        var text = "";
        foreach (var reader in readers) {
            text += reader() + ";";
        }

        return text;
    }
}
