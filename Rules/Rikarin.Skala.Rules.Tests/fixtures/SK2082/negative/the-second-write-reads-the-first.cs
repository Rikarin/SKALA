using System.Collections.Generic;

public sealed class Counters {
    public static void Bump(Dictionary<string, int> counts) {
        counts["hits"] = 1;
        counts["hits"] = counts["hits"] + 1;
    }
}
