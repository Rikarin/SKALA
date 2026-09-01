using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    public static IEnumerable<int> Recent(List<int> entries) =>
        entries.OrderBy(entry => entry).Where(entry => entry > 0);
}
