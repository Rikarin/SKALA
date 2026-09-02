using System.Collections.Generic;

public static class Returned {
    public static List<string> Collect(IEnumerable<string> items) {
        var failures = new List<string>();
        foreach (var item in items) {
            failures.Add(item);
        }

        return failures;
    }
}
