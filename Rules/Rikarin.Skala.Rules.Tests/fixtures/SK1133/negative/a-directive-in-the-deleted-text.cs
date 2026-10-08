// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Directed {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source
#if !DEBUG
            .ToArray();
#endif
        return copied;
    }
}
