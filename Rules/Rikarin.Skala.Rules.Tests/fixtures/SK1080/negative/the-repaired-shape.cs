using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static IEnumerable<string> Names(IEnumerable<object> values) => values.OfType<string>();
}
