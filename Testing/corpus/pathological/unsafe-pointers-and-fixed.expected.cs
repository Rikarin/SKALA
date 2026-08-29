// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
unsafe class C {
    void M(int[] xs) {
        fixed (int* p = xs) {
            *p = 1;
        }
    }
}
