// One overload, or a target written down: the inlined elements land in the collection the spread did,
// and in the same order. Pinned by Probe (#425).
using System.Collections.Generic;

public static class Probe {
    static string Join(List<long> values) => string.Join(",", values);

    public static string Run() {
        long[] array = [.. new long[] { 1, 2 }, 3];
        return Join([.. new long[] { 4, 5 }]) + "/" + string.Join(",", array);
    }
}
