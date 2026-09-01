using System;

class C {
    void M(int count) {
        do {
            Span<int> buffer = stackalloc[] { 1, 2, 3 };
            count -= Fill(buffer);
        } while (count > 0);
    }

    static int Fill(Span<int> buffer) => 1;
}
