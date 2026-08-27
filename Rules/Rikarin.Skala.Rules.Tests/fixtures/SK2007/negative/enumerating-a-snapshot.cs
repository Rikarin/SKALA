using System.Collections.Generic;
using System.Linq;

public sealed class SnapshotPruner {
    // The repaired form. `ToList()` returns a `List<int>` too, but it is a different object from the
    // one being modified, and the loop expression is an invocation rather than the collection.
    public void Prune(List<int> items) {
        foreach (var item in items.ToList()) {
            if (item < 0) {
                items.Remove(item);
            }
        }
    }
}
