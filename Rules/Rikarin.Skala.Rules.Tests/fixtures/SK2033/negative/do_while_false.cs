using System;

class C {
    void M() {
        do {
            Span<byte> buffer = stackalloc byte[16];
            Fill(buffer);
        } while (false);
    }

    static void Fill(Span<byte> buffer) { }
}
