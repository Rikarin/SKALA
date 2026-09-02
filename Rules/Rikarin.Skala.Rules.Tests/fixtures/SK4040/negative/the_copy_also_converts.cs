using System.Collections.Generic;
using System.Linq;

public sealed class Buffer {
    readonly List<int> values = new();

    // ⚠ `List<int>` does not convert to `int[]`, so the call is doing conversion work as well as
    // copying and no edit keeps the declared type. `CA1819` is the diagnostic for this shape, and it
    // is silent until a repository raises `AnalysisMode`.
    public int[] Values => values.ToArray();
}
