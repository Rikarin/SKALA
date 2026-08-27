using System.Collections.Generic;
using System.Linq;

public sealed class Holder {
    static List<int> Items() => new();

    public static bool Has() => Items().Any();
}
