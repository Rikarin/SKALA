// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Inferred {
    public int Count(IEnumerable<int> source) {
        var copy = source.ToArray();
        return copy.Length;
    }
}
