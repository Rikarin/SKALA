// ⚠ The trailing `break` proves nothing here: the `continue` above it reaches the stackalloc again.
using System;

class C {
    void M(bool retry) {
        while (true) {
            Span<byte> buffer = stackalloc byte[16];
            if (Fill(buffer)) {
                continue;
            }

            break;
        }
    }

    static bool Fill(Span<byte> buffer) => false;
}
