using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ `as` yields null where the cast would throw, so on a sequence the filter did not fully clean
    // this and `OfType<string>()` produce sequences of different lengths.
    public static IEnumerable<string?> Loose(IEnumerable<object> values) =>
        values.Where(value => value is string).Select(value => value as string);
}
