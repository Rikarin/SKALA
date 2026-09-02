using System.Collections.Generic;

public static class Enumerated {
    public static int Total(IEnumerable<string> items) {
        var kept = new List<string>();
        foreach (var item in items) {
            kept.Add(item);
        }

        var total = 0;
        foreach (var item in kept) {
            total += item.Length;
        }

        return total;
    }
}
