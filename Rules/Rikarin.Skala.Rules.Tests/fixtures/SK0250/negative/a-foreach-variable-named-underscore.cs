using System.Collections.Generic;

public static class Counting {
    // `foreach (var _ in …)` declares a local actually named `_`; it is not a pattern at all,
    // and deleting anything here deletes the loop variable.
    public static int Count(IEnumerable<int> values) {
        var total = 0;
        foreach (var _ in values) {
            total++;
        }

        return total;
    }
}
