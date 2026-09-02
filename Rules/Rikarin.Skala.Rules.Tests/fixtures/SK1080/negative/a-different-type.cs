using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // The predicate and the cast disagree, so the two operators are not one operator: `OfType<object>`
    // would keep every element and this keeps the strings.
    public static IEnumerable<object> Widened(IEnumerable<object> values) =>
        values.Where(value => value is string).Cast<object>();
}
