// fixture-option: LangVersion = 14
// One defect, one finding: SK1081 deletes the inner `ToList()`, and this rule takes `source.ToArray()`
// on the next pass.
using System.Collections.Generic;
using System.Linq;

public sealed class Twice {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source.ToList().ToArray();
        return copied;
    }
}
