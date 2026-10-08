// fixture-option: LangVersion = 14
// `M([..source])` is ambiguous between `int[]` and `List<int>` (CS0121): the call's own type is what
// picked the overload.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static string M(int[] values) => "array";

    static string M(List<int> values) => "list";

    public static string Run() {
        IEnumerable<int> source = new[] { 1, 2 };
        return M(source.ToArray()) + "/" + M(source.ToList());
    }
}
