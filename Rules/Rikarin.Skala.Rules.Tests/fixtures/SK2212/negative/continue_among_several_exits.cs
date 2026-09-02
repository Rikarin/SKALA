// ⚠ One `continue` among several exits is enough to withdraw the finding: that path alone goes
// round again, whatever the others do.
using System.Collections.Generic;

class C {
    int M(List<int> items) {
        foreach (var item in items) {
            if (item < 0) {
                continue;
            }

            return item;
        }

        return -1;
    }
}
