// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
unsafe struct S {
    public int Value;
}

unsafe class C {
    int M(S* p) => p->Value;
}
