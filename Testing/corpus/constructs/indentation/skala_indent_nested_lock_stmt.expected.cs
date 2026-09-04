// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class C {
    readonly object _a = new();
    readonly object _b = new();

    void M() {
        lock (_a)
        lock (_b) {
            M();
        }
    }
}
