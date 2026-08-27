class C {
    void M() {
        Inner();

        void Inner() {
            M();
        }
    }
}
