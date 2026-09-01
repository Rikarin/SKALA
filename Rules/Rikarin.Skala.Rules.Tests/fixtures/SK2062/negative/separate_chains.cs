// The same condition in two different chains is two decisions, and neither shadows the other.
class C {
    void M(bool ready) {
        if (ready) {
            A();
        } else if (!ready) {
            B();
        }
    }

    void N(bool ready) {
        if (ready) {
            A();
        } else if (!ready) {
            B();
        }
    }

    static void A() { }

    static void B() { }
}
