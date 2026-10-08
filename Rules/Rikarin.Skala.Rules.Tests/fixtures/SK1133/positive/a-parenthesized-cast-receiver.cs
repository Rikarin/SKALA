// fixture-option: LangVersion = 14
// `((IEnumerable<int>)list).ToArray()` calls `Enumerable.ToArray`, and so does `[..(IEnumerable<int>)list]`:
// the cast decides the lowering, so it stays. Only the parentheses the spread does not need go.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static List<int> Missing() => null!;

    public static string Run() {
        var list = new List<int> { 1, 2 };
        int[] copied = ((IEnumerable<int>)list).ToArray();
        string thrown;
        try {
            int[] none = ((IEnumerable<int>)Missing()).ToArray();
            thrown = none.Length.ToString();
        } catch (System.Exception exception) {
            thrown = exception.GetType().Name;
        }

        return string.Join(",", copied) + "/" + thrown;
    }
}
