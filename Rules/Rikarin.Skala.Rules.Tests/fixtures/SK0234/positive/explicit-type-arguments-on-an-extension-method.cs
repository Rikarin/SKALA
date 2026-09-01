using System.Collections.Generic;
using System.Linq;

public static class Materialize {
    public static List<int> AsList(IEnumerable<int> values) => values.ToList<int>();
}
