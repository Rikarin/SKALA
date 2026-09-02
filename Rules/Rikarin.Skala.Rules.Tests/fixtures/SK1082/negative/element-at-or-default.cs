using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // Returns default(T) where the indexer throws: there is no indexer expression that means this.
    public static int Third(List<int> entries) => entries.ElementAtOrDefault(2);
}
