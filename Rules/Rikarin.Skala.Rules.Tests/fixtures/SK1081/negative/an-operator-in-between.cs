using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static List<int> Ids(IEnumerable<int> source) => source.ToList().Distinct().ToList();
}
