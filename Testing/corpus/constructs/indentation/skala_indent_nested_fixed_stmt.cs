unsafe class C {
    void M(int[] xs) {
        fixed (int* p = xs)
        fixed (int* q = xs) {
            M(xs);
        }
    }
}
