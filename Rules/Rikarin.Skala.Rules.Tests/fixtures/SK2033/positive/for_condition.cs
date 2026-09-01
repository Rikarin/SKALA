// A `for` re-evaluates its condition and its incrementors, unlike its initializer.
using System;

class C {
    void M() {
        for (var i = 0; Fill(stackalloc byte[8]); i++) {
        }
    }

    static bool Fill(Span<byte> buffer) => false;
}
