using System;

class C {
    void M(int count) {
        for (var i = 0; i < count; i++) {
            Run();
        }

        static void Run() {
            Span<byte> buffer = stackalloc byte[16];
            Fill(buffer);
        }
    }

    static void Fill(Span<byte> buffer) { }
}
