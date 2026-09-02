using System.Collections.Generic;

public static class Seen {
    public static string Describe(IEnumerable<string> items) {
        var seen = new HashSet<string>();
        var last = string.Empty;

        foreach (var item in items) {
            seen.Add(item);
            last = item;
        }

        seen.Clear();
        return last;
    }
}
