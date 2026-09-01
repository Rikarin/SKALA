using System;

class C {
    void M(int count) {
        for (var i = 0; i < count; i++) {
            Span<byte> buffer = stackalloc byte[16];
            throw new InvalidOperationException(Describe(buffer));
        }
    }

    static string Describe(Span<byte> buffer) => "";
}
