// ⚠ #430: a getter that is not storage is a method call, and the rule cannot see what it does to the list.
using System.Collections.Generic;
using System.Linq;

public sealed class Cursor {
    readonly int[] values = { 5, 2, 9 };
    int index;

    public int Next => values[index++];
}

public sealed class Registry {
    public static bool Seen(List<int> list, Cursor cursor) => list.Any(x => x == cursor.Next);
}
