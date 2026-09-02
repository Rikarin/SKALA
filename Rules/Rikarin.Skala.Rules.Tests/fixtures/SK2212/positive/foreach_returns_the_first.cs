using System.Collections.Generic;

class C {
    int First(List<int> items) {
        foreach (var item in items) {
            return item;
        }

        return -1;
    }
}
