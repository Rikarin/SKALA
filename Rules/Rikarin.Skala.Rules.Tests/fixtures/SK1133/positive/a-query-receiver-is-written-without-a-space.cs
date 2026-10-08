// fixture-option: LangVersion = 14
// The receiver takes a lambda and is still written glued: `[..source.Where(value => value > 0)]`.
using System.Collections.Generic;
using System.Linq;

public sealed class Filter {
    public int[] Positive(IEnumerable<int> source) {
        int[] kept = source.Where(value => value > 0).ToArray();
        return kept;
    }
}
