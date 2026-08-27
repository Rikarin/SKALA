// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
unsafe struct S {
    public int Value;
}

unsafe class C {
    int M(S* p) => p->Value;
}
