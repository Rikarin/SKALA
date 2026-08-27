// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
class C {
    void M() {
        M();

        void Inner() {
            M();
        }

        Inner();
    }
}
