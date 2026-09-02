using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static IEnumerable<int> Ids(IEnumerable<object> values) =>
        values.Where(value => value is int).Select(value => (int)value);
}
