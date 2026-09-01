using System;

class C {
    void M(int count) {
        for (var i = 0; i < count; i++) {
            Span<byte> buffer = stackalloc byte[64];
            Fill(buffer);
        }
    }

    static void Fill(Span<byte> buffer) { }
}
