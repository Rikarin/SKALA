// A loop with no statements has no jump in it, and an empty loop is a different finding.
using System.Collections.Generic;

class C {
    void M(List<int> items) {
        foreach (var item in items) { }
    }
}
