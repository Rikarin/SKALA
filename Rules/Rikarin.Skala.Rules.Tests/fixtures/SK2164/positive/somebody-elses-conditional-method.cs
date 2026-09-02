using System.Collections.Generic;
using System.Diagnostics;

// ⚠ The defect is a property of the attribute, not of the framework's two assertion methods. A
// repository's own conditional helper deletes its arguments exactly the same way.
public sealed class Tracker {
    readonly HashSet<int> pending = [];

    [Conditional("TRACE")]
    static void Check(bool condition) { }

    public void Complete(int id) {
        Check(pending.Remove(id));
    }
}
