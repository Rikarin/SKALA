// ⚠ #423: `Any(x => x == cursor.Next)` reads the getter once per element and `Contains(cursor.Next)`
// would read it once — measured for #412's audit as `True` before and `False` after. The rewrite to
// `Contains` is declined, and the one left is `Exists`, which keeps the lambda and so the reads.
// ⚠ #430: an `ImmutableList<T>`, because over a `List<T>` a getter is code that could grow the list
// and the rule now declines that predicate outright (`a-getter-in-the-predicate`).
using System.Collections.Immutable;
using System.Linq;

public sealed class Cursor {
    readonly int[] values = { 5, 2, 9 };
    int index;

    public int Next => values[index++];
}

public static class Probe {
    public static bool Run() {
        var cursor = new Cursor();
        var list = ImmutableList.Create(1, 2, 3);
        return list.Any(x => x == cursor.Next);
    }
}
