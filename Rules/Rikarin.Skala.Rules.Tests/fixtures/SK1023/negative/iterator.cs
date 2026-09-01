using System.Collections.Generic;

class C {
    readonly object gate = new();

    IEnumerable<int> M() {
        lock (gate) {
            yield return 1;
        }
    }
}
