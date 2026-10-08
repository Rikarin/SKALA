// fixture-option: LangVersion = 14
// The list a spread builds is the list `ToList` builds — it is `ToList` — capacity included.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static IEnumerable<int> Three() {
        yield return 1;
        yield return 2;
        yield return 3;
    }

    public static string Run() {
        List<int> lazy = Three().ToList();
        var source = new List<int> { 1, 2, 3, 4, 5 };
        List<int> counted = source.ToList();
        counted.Add(6);
        return string.Join(",", lazy) + "/" + lazy.Capacity + "/" + source.Count + "/" + counted.Capacity;
    }
}
