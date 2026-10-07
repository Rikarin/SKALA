// ⚠ #425: `ToList<object>()` on a `List<string>` makes a list of `object`, and the array copied out of
// it is an `object[]`; without it, an array of `string` that throws on an `int` write. Measured for
// #412's audit: `stored 1` before the fix and `ArrayTypeMismatchException` after it. The guard compared
// a node with itself, so it held for every call.
using System;
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    public static string Run() {
        var words = new List<string> { "a", "b" };
        object[] array = words.ToList<object>().ToArray();
        try {
            array[0] = 1;
            return "stored " + array[0];
        } catch (ArrayTypeMismatchException) {
            return "mismatch";
        }
    }
}
