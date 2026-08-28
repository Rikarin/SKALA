// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
unsafe struct S {
    public int Value;
}

unsafe class C {
    int M(S* p) => p->Value;
}
