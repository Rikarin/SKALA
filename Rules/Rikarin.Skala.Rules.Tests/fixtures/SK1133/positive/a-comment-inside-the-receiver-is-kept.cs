// fixture-option: LangVersion = 14
// The receiver is carried across as text, so a comment inside it survives; only `.ToArray()` is deleted.
using System.Collections.Generic;
using System.Linq;

public sealed class Kept {
    public int[] Even(IEnumerable<int> source) {
        int[] even = source.Where(value => /* parity */ value % 2 == 0).ToArray();
        return even;
    }
}
