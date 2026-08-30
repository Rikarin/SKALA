// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class C {
    void M(int[] xs) {
        foreach (var x in xs)
        foreach (var y in xs) {
            M(xs);
        }
    }
}
