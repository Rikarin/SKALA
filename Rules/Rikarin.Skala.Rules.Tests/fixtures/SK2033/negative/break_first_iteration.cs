// The "loop as a labelled block" idiom: the body cannot reach a second iteration.
using System;

class C {
    void M() {
        while (true) {
            Span<byte> buffer = stackalloc byte[16];
            Fill(buffer);
            break;
        }
    }

    static void Fill(Span<byte> buffer) { }
}
