// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
unsafe struct S {
    public int Value;
}

unsafe class C {
    int M(S* p) => p -> Value;
}
