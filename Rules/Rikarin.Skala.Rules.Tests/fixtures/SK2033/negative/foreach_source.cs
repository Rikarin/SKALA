// The source expression of a `foreach` is evaluated once, before the first iteration.
using System;

class C {
    void M() {
        foreach (var b in stackalloc byte[8]) {
            Use(b);
        }
    }

    static void Use(byte value) { }
}
