// A `for` initializer runs exactly once, unlike its condition and its incrementors.
using System;

class C {
    void M() {
        for (var i = Fill(stackalloc byte[8]); i < 4; i++) {
        }
    }

    static int Fill(Span<byte> buffer) => 0;
}
