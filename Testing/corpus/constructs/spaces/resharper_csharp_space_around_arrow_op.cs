unsafe struct S {
    public int Value;
}

unsafe class C {
    int M(S* p) => p->Value;
}
