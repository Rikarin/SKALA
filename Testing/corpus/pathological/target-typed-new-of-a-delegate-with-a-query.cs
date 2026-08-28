using System;
using System.Linq;

class C {
    void M() {
        Func<int> v = new () { P = (from item in items select null) };
    }
}
