using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // The type test is not the whole predicate, so the filter selects fewer elements than OfType does.
    public static IEnumerable<string> Long(IEnumerable<object> values) =>
        values.Where(value => value is string && value.ToString()!.Length > 3).Cast<string>();
}
