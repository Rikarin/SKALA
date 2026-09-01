// An indexer is a call. Declining it is an under-report and the safe direction.
class C {
    void M(bool[] items) {
        if (items[0]) {
            A();
        } else if (items[0]) {
            B();
        }
    }

    static void A() { }

    static void B() { }
}
