class C {
#if DEBUG
    void M(int a) {
#else
    void M(int a, int b) {
#endif
        M(a);
    }
}
