// The shape most of a repository is made of. A metric rule that fires here is a metric rule that
// gets switched off (docs/plan/16 § R3).
using System.Collections.Generic;

public sealed class OrdinaryMethod {
    public static int Sum(IReadOnlyList<int> values, bool positiveOnly) {
        var total = 0;
        foreach (var value in values) {
            if (positiveOnly && value < 0) {
                continue;
            }

            total += value;
        }

        return total;
    }
}
