using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    public static IEnumerable<int> Recent(List<string> entries) =>
        entries.OrderBy(entry => entry).Select(entry => entry.Length).Where(length => length > 0);
}
