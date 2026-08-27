class C {
    void M(int a) {
        M(a);
        M();
    }

    void M() { }
}
