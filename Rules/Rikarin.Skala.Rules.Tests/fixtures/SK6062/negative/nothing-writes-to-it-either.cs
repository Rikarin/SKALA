using System.Collections.Generic;

// A collection that is never written and never read is an unused local, which is a different
// finding. This rule requires at least one write.
public static class Untouched {
    public static int Run() {
        var unused = new List<string>();
        return 0;
    }
}
