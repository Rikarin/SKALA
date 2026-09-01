using System.Collections.Generic;

public sealed class Counters {
    // `+=` reads before it writes, so neither statement is a candidate.
    public static void Bump(Dictionary<string, int> counts) {
        counts["hits"] = 1;
        counts["hits"] += 1;
    }
}
