class C {
    void M() {
#pragma warning disable CA1822
        M();
#pragma warning restore CA1822
    }
}
