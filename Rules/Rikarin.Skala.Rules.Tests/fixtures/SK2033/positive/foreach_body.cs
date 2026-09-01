using System;

class C {
    void M(int[] items) {
        foreach (var item in items) {
            Span<byte> buffer = stackalloc byte[32];
            Fill(buffer);
        }
    }

    static void Fill(Span<byte> buffer) { }
}
