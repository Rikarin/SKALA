// fixture-option: LangVersion = 14
// `List<T>.ToArray()` is the call `[..list]` lowers to, so the copy is the same object kind, length
// and order, and a null list throws the same NullReferenceException.
using System.Collections.Generic;

public sealed class Snapshot {
    public int[] Take(List<int> list) {
        int[] taken = list.ToArray();
        return taken;
    }
}
