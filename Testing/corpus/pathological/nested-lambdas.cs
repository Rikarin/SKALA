using System;

class C {
    Func<int, Func<int, int>> M() => a => b => a + b;
}
