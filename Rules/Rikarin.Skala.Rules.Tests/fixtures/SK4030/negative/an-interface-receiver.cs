using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // IList<T> and IEnumerable<T> declare none of the three members.
    public static bool AnyReady(IList<int> values) => values.Any(value => value > 0);

    public static bool AllReady(IEnumerable<int> values) => values.All(value => value > 0);
}
