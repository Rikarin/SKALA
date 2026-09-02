using System.Collections.Generic;

class C {
    int M(List<int> items) {
        foreach (var item in items) {
            if (item > 0) {
                return item;
            }

            break;
        }

        return 0;
    }
}
