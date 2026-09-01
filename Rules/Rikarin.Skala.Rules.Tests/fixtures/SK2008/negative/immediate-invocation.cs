using System;

class C {
    void M() {
        for (var i = 0; i < 3; i++) {
            ((Action)(() => Console.WriteLine(i)))();
        }
    }
}
