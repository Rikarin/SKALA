// The loop that works: the jump is under a condition, so the body can complete and go round again.
using System.Collections.Generic;

class C {
    int Find(List<int> items) {
        foreach (var item in items) {
            if (item > 10) {
                return item;
            }
        }

        return -1;
    }
}
