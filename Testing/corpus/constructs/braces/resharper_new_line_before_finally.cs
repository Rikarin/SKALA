class C {
    void M() {
        try {
            M();
        } finally {
            M();
        }
    }
}
