// ⚠ Structures, not braces: `while`, `foreach`, `try`, `lock`, `using` and `if` each add a level, and
// the last `if` has no braces at all. Seven levels.
using System;
using System.Collections.Generic;

public sealed class MixedStructures {
    static readonly object Gate = new object();

    public static int Count(IEnumerable<int> values, bool a, bool b) {
        var total = 0;
        while (a) {
            foreach (var value in values) {
                try {
                    lock (Gate) {
                        using (var reader = new System.IO.StringReader("x")) {
                            if (b) {
                                if (value > 0)
                                    total += reader.Read();
                            }
                        }
                    }
                } catch (InvalidOperationException) {
                    total = 0;
                }
            }

            a = false;
        }

        return total;
    }
}
