using System.Collections.Generic;

public static class Counted {
    public static int HowMany(IEnumerable<string> items) {
        var kept = new List<string>();
        foreach (var item in items) {
            kept.Add(item);
        }

        return kept.Count;
    }
}
