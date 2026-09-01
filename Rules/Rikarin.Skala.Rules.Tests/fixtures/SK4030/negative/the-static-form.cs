using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // The collection is in the argument list, so the fix would be a move rather than a rename.
    public static bool AnyReady(List<int> values) => Enumerable.Any(values, value => value > 0);
}
