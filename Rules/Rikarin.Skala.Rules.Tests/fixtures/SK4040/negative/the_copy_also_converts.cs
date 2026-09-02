using System.Collections.Generic;
using System.Linq;

public sealed class Buffer {
    readonly IEnumerable<int> values = new List<int>();

    // ⚠ This file exists because sabotaging the conversion test turned nothing red: the fixture that
    // was meant to witness it used `List<int>.ToArray()`, an *instance* method, and was being
    // declined by the Enumerable-binding test instead. Here the call is `Enumerable.ToArray`, so only
    // the conversion test declines it: `IEnumerable<int>` does not convert to `int[]`, the call is
    // doing conversion work as well as copying, and no edit keeps the declared type. `CA1819` is the
    // diagnostic for this shape, and it is silent until a repository raises `AnalysisMode`.
    public int[] Values => values.ToArray();
}
