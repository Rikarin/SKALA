using System.Collections.Generic;

class C {
    void M(List<int> items) {
        foreach (var item in items) {
            System.Console.WriteLine(item);
            break;
        }
    }
}
