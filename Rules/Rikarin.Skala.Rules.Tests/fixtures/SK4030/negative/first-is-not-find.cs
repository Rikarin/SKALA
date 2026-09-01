using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // `First` throws on no match where `Find` returns default; two programs, not two spellings.
    public static int Ready(List<int> values) => values.First(value => value > 0);

    // `SingleOrDefault` throws on a *second* match, which `Find` never does.
    public static int Only(List<int> values) => values.SingleOrDefault(value => value > 0);
}
