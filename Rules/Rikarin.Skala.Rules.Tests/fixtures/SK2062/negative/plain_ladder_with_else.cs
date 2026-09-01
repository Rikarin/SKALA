enum Mode { Fast, Careful, Off }

class C {
    void M(Mode mode) {
        if (mode == Mode.Fast) {
            A();
        } else if (mode == Mode.Careful) {
            B();
        } else {
            D();
        }
    }

    static void A() { }

    static void B() { }

    static void D() { }
}
