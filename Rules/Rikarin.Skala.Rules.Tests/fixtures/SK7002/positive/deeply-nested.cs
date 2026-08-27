// 1 + 2 + 3 + 4 + 5 + 6 = 21 against a default threshold of 15. The same six conditions written flat
// would score 6; the difference is the whole point of the metric.
public sealed class DeeplyNested {
    public static int Walk(int[] values, bool a, bool b, bool c, bool d, bool e) {
        var total = 0;
        if (a) {
            foreach (var value in values) {
                if (b) {
                    while (c) {
                        if (d) {
                            if (e) {
                                total += value;
                            }
                        }
                    }
                }
            }
        }

        return total;
    }
}
