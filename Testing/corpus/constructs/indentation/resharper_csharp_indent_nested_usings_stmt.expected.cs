// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
using System;

class C {
    void M(IDisposable d) {
        using (d)
        using (d) {
            M(d);
        }
    }
}
