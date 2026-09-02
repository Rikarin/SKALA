using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ ToHashSet removes duplicates, so the inner call is a real operation and deleting it would
    // change the result. It is allowed as the outer call and never as the inner one.
    public static List<int> Ids(IEnumerable<int> source) => source.ToHashSet().ToList();
}
