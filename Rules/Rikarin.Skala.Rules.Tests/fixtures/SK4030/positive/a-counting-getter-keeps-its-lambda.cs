// ⚠ #423: `Any(x => x == cursor.Next)` reads the getter once per element and `Contains(cursor.Next)`
// would read it once — measured for #412's audit as `True` before and `False` after. The rewrite to
// `Contains` is declined, and the one left is `Exists`, which keeps the lambda and so the reads.
using System.Collections.Generic;
using System.Linq;

public sealed class Cursor {
    readonly int[] values = { 5, 2, 9 };
    int index;

    public int Next => values[index++];
}

public static class Probe {
    public static bool Run() {
        var cursor = new Cursor();
        var list = new List<int> { 1, 2, 3 };
        return list.Any(x => x == cursor.Next);
    }
}
