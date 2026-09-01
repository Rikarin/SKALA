using System;

class C {
    int M(int[] items) {
        foreach (var item in items) {
            Span<byte> buffer = stackalloc byte[16];
            return Fill(buffer) + item;
        }

        return 0;
    }

    static int Fill(Span<byte> buffer) => 0;
}
