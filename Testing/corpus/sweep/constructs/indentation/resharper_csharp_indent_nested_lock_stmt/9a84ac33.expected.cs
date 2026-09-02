// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
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
