using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static int[]? Ids(IEnumerable<int>? source) => source?.ToList().ToArray();
}
