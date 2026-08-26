// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
unsafe class C {
    void M(int[] xs) {
        fixed (int* p = xs) {
            *p = 1;
        }
    }
}
