// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class C {
    void M(bool b, int[] xs) {
        if (b) {
            M(b, xs);
        }

        foreach (var x in xs) {
            M(b, xs);
        }

        for (var i = 0; i < xs.Length; i++) {
            M(b, xs);
        }
    }
}
