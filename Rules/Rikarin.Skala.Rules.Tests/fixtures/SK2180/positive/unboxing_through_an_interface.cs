using System;
using System.Collections.Generic;

static class Sum {
    // An unboxing conversion per element, and every element that is not an `int` throws.
    public static int Of(List<IComparable> values) {
        var total = 0;
        foreach (int value in values) {
            total += value;
        }

        return total;
    }
}
