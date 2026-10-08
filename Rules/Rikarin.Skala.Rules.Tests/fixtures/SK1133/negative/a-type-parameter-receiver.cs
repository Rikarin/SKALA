// fixture-option: LangVersion = 14
// A type parameter is enumerated into a fresh List<T> by the lowering, not passed to Enumerable.ToArray.
using System.Collections.Generic;
using System.Linq;

public static class Generic {
    public static int[] Copy<T>(T source) where T : IEnumerable<int> {
        int[] copied = source.ToArray();
        return copied;
    }
}
