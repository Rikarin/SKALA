// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
using System;

class C {
    void M(IDisposable d) {
        using (d)
        using (d) {
            M(d);
        }
    }
}
