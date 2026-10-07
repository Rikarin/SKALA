// ⚠ #425: with `ToList()` gone, `ToArray()` is looked up on the receiver, which declares its own.
// Measured for #412's audit: `1,2` before the fix and `42` after it.
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public sealed class Bag : IEnumerable<int> {
    public int[] ToArray() => new[] { 42 };

    public IEnumerator<int> GetEnumerator() {
        yield return 1;
        yield return 2;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class Probe {
    public static string Run() => string.Join(",", new Bag().ToList().ToArray());
}
