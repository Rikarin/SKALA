// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
unsafe class C {
    void M(int[] xs) {
        fixed (int* p = xs)
            fixed (int* q = xs) {
                M(xs);
            }
    }
}
