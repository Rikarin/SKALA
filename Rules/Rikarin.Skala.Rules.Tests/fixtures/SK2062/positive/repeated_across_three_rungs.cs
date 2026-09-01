class C {
    void M(int n) {
        if (n < 0) {
            A();
        } else if (n == 0) {
            B();
        } else if (n < 0) {
            D();
        }
    }

    static void A() { }

    static void B() { }

    static void D() { }
}
