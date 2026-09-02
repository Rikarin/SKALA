using System.Collections.Generic;

public static class Passed {
    static int Consume(IReadOnlyCollection<string> values) => values.Count;

    public static int Run(IEnumerable<string> items) {
        var kept = new List<string>();
        foreach (var item in items) {
            kept.Add(item);
        }

        return Consume(kept);
    }
}
