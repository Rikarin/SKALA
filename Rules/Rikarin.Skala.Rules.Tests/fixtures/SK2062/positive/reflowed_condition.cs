// Structural equality ignores layout, so a condition reflowed across lines still matches.
class C {
    void M(bool ready, bool loaded) {
        if (ready && loaded) {
            A();
        } else if (ready
            && loaded) {
            B();
        }
    }

    static void A() { }

    static void B() { }
}
