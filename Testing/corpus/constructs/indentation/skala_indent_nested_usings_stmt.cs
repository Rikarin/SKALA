using System;

class C {
    void M(IDisposable d) {
        using (d)
        using (d) {
            M(d);
        }
    }
}
