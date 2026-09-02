using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // `is string text` is an IsPatternExpression, not an IsExpression: the name it introduces would
    // have nowhere to go once the lambda is deleted.
    public static IEnumerable<string> Declared(IEnumerable<object> values) =>
        values.Where(value => value is string text && text.Length > 0).Cast<string>();
}
