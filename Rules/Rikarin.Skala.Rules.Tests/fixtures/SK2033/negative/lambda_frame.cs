// A lambda is a separate frame: its stack is released when it returns, however often it is called.
using System;

class C {
    void M(int count) {
        for (var i = 0; i < count; i++) {
            Action run = () => {
                Span<byte> buffer = stackalloc byte[16];
                Fill(buffer);
            };
            run();
        }
    }

    static void Fill(Span<byte> buffer) { }
}
