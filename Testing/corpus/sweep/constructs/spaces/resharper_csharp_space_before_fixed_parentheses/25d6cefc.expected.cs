// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
unsafe class C {
    int _field;

    void M() {
        fixed(int* p = &_field) {
            System.Console.Write(*p);
        }
    }
}
