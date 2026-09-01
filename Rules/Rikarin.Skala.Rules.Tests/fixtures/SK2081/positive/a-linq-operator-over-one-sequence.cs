using System.Collections.Generic;
using System.Linq;

public sealed class Report {
    public static IEnumerable<string> Missing(List<string> wanted) => wanted.Except(wanted);
}
