using System.Collections.Generic;

static class Pairs {
    // A deconstructing `foreach` is a different statement kind and has no narrowing to report.
    public static int Total(List<(string Key, int Value)> pairs) {
        var total = 0;
        foreach (var (_, value) in pairs) {
            total += value;
        }

        return total;
    }
}
