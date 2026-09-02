using System.Collections.Generic;

static class Totals {
    // A narrowing *numeric* conversion cannot throw outside a `checked` context, so it is a
    // different concept: only reference conversions and unboxings reach a report.
    public static int Of(List<long> values) {
        var total = 0;
        foreach (int value in values) {
            total += value;
        }

        return total;
    }
}
