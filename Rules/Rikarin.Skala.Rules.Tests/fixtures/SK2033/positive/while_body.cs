using System;

class C {
    void M(bool more) {
        while (more) {
            Span<byte> buffer = stackalloc byte[16];
            more = Fill(buffer);
        }
    }

    static bool Fill(Span<byte> buffer) => false;
}
