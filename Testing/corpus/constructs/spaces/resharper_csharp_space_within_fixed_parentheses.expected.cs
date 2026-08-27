// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
unsafe class C {
    int _field;

    void M() {
        fixed (int* p = &_field) {
            System.Console.Write(*p);
        }
    }
}
