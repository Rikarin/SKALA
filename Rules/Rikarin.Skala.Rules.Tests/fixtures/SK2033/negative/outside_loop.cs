using System;

class C {
    void M(int count) {
        Span<byte> buffer = stackalloc byte[64];
        for (var i = 0; i < count; i++) {
            Fill(buffer);
        }
    }

    static void Fill(Span<byte> buffer) { }
}
