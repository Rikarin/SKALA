// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class C {
#if DEBUG
    void M(int a) {
#else
    void M(int a, int b) {
#endif
        M(a);
    }
}
