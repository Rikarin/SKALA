// ⚠ #425: the spread of a `long[]` offers only `long`s, so only `M(List<long>)` applies; the literals it
// held convert to `int` too, and `M(List<int>)` is the better conversion. Measured for #412's audit:
// `long` and `object[]` before the fix, `int` and `int[]` after it.
using System.Collections.Generic;

public static class Probe {
    static string M(List<int> values) => "int";

    static string M(List<long> values) => "long";

    static string N(int[] values) => "int[]";

    static string N(object[] values) => "object[]";

    public static string Run() => M([.. new long[] { 1, 2 }]) + "/" + N([.. new object[] { 1, 2 }]);
}
