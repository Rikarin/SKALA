using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    public static IEnumerable<int> Recent(List<int> entries) =>
        entries.Where(entry => entry > 0).OrderBy(entry => entry);
}
