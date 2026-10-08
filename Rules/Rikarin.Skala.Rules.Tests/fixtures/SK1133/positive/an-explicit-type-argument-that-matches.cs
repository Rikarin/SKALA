// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Ids {
    public long[] Copy(IReadOnlyList<long> ids) {
        long[] copied = ids.ToArray<long>();
        return copied;
    }
}
