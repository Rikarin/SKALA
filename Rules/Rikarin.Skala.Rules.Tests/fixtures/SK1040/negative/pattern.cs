using System;
using System.Collections.Generic;

// Nothing inside a pattern is rewritten. C# rejects a nullable value type in a pattern outright,
// and the rule declines the whole position rather than reasoning about which nesting is safe.
public sealed class Matching {
    public static int Count(object value) =>
        value is List<Nullable<int>> points ? points.Count : 0;
}
