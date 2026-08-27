// ⚠ The paper's arithmetic in one method: `if` +1, the `&&`/`||` mix +2, the nested loop +2, the
// nested `if` +3 and its mixed condition +2, the `catch` +1 and the `if` inside it +2, the `else`
// +1 and the ternary under it +3. Seventeen, over a threshold of fifteen.
using System;

public sealed class NestingAndMixedOperators {
    public static int Run(int[] values, bool a, bool b, bool c) {
        var total = 0;
        try {
            if (a && b || c) {
                foreach (var value in values) {
                    if (a || b && c) {
                        total += value;
                    }
                }
            } else {
                total = a ? 1 : 2;
            }
        } catch (InvalidOperationException) {
            if (b) {
                total = 0;
            }
        }

        return total;
    }
}
