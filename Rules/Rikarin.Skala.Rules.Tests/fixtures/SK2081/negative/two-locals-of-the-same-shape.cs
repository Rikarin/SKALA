using System.Collections.Generic;
using System.Linq;

public sealed class Report {
    public static bool Same(List<string> rows) {
        var copy = rows.ToList();
        return rows.SequenceEqual(copy);
    }
}
