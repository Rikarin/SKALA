// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
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
