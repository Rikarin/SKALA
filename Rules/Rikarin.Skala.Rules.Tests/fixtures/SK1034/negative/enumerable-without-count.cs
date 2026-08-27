using System.Collections.Generic;
using System.Linq;

public sealed class Holder {
    public static bool Has(IEnumerable<int> items) => items.Any();
}
