// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-26
unsafe class C {
    void M(int[] xs) {
        fixed (int* p = xs) {
            *p = 1;
        }
    }
}
