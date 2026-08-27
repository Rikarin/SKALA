class C {
    void M() {
        try {
            M();
        } catch (System.Exception) {
            M();
        } finally {
            M();
        }
    }
}
