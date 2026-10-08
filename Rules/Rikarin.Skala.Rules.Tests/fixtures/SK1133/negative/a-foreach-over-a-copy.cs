// fixture-option: LangVersion = 14
// No target type, and SK4006's shape: `foreach (var x in [..xs])` is CS9176.
using System.Collections.Generic;
using System.Linq;

public sealed class Loop {
    readonly List<int> values = new() { 1, 2 };

    public int Sum() {
        var total = 0;
        foreach (var value in values.ToArray()) {
            total += value;
            values.Add(value);
        }

        return total;
    }
}
