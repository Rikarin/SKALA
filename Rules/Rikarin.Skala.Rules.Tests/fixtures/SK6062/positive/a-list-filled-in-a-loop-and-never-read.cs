using System.Collections.Generic;

public static class Validation {
    public static int CountAll(IEnumerable<string> items) {
        var failures = new List<string>();
        var total = 0;

        foreach (var item in items) {
            total++;
            if (item.Length == 0) {
                failures.Add(item);
            }
        }

        return total;
    }
}
