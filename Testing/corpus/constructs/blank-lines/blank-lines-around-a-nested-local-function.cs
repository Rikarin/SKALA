class C {
    void M() {
        void A() {
            void B() {
                M();
            }

            B();
        }

        A();
    }
}
