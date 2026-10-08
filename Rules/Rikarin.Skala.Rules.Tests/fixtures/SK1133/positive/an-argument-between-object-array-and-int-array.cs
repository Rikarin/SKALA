// fixture-option: LangVersion = 14
// `M(object[])` beside `M(int[])`: the call picks `int[]`, and so does `[..source]` — an `int` converts to
// `int` better than to `object`. FixRebind proves the same overload, and Probe pins it (#512).
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static string M(object[] values) => "object[]";

    static string M(int[] values) => "int[]";

    public static string Run() {
        IEnumerable<int> source = new List<int> { 1, 2 };
        return M(source.ToArray());
    }
}
