class C {
    void M() {
        M();

        void Inner() {
            M();
        }

        Inner();
    }
}
