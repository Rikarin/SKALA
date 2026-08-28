using System.Collections.Generic;

class C {
    IEnumerable<int> M(int a) {
        var x = 1;
        yield return x;
        var y = 2;
        yield return y;
    }
}
