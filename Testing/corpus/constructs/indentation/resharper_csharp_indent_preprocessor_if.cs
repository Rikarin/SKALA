class C {
    void M() {
#if DEBUG
        M();
#endif
    }
}
