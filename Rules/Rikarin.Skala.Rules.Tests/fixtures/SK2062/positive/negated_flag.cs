class C {
    void M(bool ready) {
        if (!ready) {
            A();
        } else if (!ready) {
            B();
        }
    }

    static void A() { }

    static void B() { }
}
