// ⚠ #423: over an empty list the lambda never runs, so `holder.Value` is never read; `Contains` would
// read it once and throw on a null `holder`. That rewrite is declined, and `Exists` keeps the lambda.
using System.Collections.Generic;
using System.Linq;

public sealed class Holder {
    public int Value;
}

public static class Probe {
    static bool Known(List<int> list, Holder holder) => list.Any(x => x == holder.Value);

    public static bool Run() => Known(new List<int>(), null!);
}
