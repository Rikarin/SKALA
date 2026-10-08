// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Measured {
    public int Count(IEnumerable<int> source) {
        int count = source.ToArray().Length;
        return count;
    }
}
