unsafe class C {
    int* M(ref int a) {
        fixed (int* p = &a) {
            return p;
        }
    }
}
