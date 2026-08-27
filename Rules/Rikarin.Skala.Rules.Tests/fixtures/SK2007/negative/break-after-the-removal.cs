using System.Collections.Generic;
using System.Linq;

public sealed class FirstNegativeRemover {
    // ⚠ The one legal spelling: remove and leave. `MoveNext` is never called again, so the version
    // counter never matters, and this is the shape the rule must not touch.
    public void RemoveFirstNegative(List<int> items) {
        foreach (var item in items) {
            if (item < 0) {
                items.Remove(item);
                break;
            }
        }
    }
}
