unsafe class C {
    void M(int[] xs) {
        fixed (int* p = xs) {
            *p = 1;
        }
    }
}
