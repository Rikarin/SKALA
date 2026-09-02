// ⚠ An unreachable endpoint is not by itself a body that jumps out. Control also fails to reach
// the end when a statement never completes, and in practice that is a nested constant-condition
// loop — so a body containing one is declined rather than reported for the wrong reason.
using System.Collections.Generic;

class C {
    void M(List<int> items) {
        foreach (var item in items) {
            while (true) {
                System.Console.WriteLine(item);
            }
        }
    }
}
