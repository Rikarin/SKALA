// fixture-option: LangVersion = 14
// Each branch alone would compile — the other gives the `?:` its type — but both rewritten do not.
using System.Collections.Generic;
using System.Linq;

public sealed class Choose {
    public int[] Pick(bool first, IEnumerable<int> left, IEnumerable<int> right) {
        int[] picked = first ? left.ToArray() : right.ToArray();
        return picked;
    }
}
