using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ The `Func<T, int, bool>` overload has no OfType counterpart: the index it hands the predicate
    // does not exist in the single-operator form.
    public static IEnumerable<string> Positioned(IEnumerable<object> values) =>
        values.Where((value, position) => value is string && position > 0).Cast<string>();
}
