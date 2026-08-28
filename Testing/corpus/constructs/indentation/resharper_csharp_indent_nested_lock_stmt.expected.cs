// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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
