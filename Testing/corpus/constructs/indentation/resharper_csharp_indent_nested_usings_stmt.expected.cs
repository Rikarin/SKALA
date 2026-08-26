// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
using System;

class C {
    void M(IDisposable d) {
        using (d)
        using (d) {
            M(d);
        }
    }
}
