using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // Collapsing this would mean moving the member binding rather than replacing a name.
    public static IEnumerable<string>? Maybe(IEnumerable<object>? values) =>
        values?.Where(value => value is string).Cast<string>();
}
