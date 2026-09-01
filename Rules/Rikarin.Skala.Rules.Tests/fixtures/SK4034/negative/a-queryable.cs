using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    public static IEnumerable<int> Recent(List<int> entries) =>
        entries.AsQueryable().OrderBy(entry => entry).Where(entry => entry > 0);
}
