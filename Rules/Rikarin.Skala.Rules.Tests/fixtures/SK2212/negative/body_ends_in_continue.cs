// ⚠ `continue` ends the iteration and not the loop, so this body has an unreachable endpoint and
// still runs to completion. An endpoint check on its own would report it.
using System.Collections.Generic;

class C {
    void M(List<int> items) {
        foreach (var item in items) {
            System.Console.WriteLine(item);
            continue;
        }
    }
}
