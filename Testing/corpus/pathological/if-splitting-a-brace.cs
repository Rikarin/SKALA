class C {
    void M(bool b) {
#if DEBUG
        if (b) {
#else
        if (!b) {
#endif
            M(b);
        }
    }
}
