using System.Collections.Generic;
using System.Linq;

public sealed class Report {
    public static bool Unchanged(List<string> rows) => Enumerable.SequenceEqual(rows, rows);
}
