// The ordinary loop, whose endpoint is reachable.
using System.Collections.Generic;

class C {
    void M(List<int> items) {
        foreach (var item in items) {
            System.Console.WriteLine(item);
        }

        for (var i = 0; i < 10; i++) {
            System.Console.WriteLine(i);
        }
    }
}
