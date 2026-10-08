// fixture-option: LangVersion = 14
// One overload: the spread lands in the `int[]` the call landed in. FixRebind proves it.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static string Describe(int[] values) => "int[]:" + string.Join(",", values);

    public static string Run() {
        IEnumerable<int> source = new List<int> { 3, 1, 2 };
        return Describe(source.ToArray());
    }
}
