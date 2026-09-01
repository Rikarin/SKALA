// The condition moves the state it reads, so the second test is a different question.
class C {
    void M(int i) {
        if (i++ > 0) {
            A();
        } else if (i++ > 0) {
            B();
        }
    }

    static void A() { }

    static void B() { }
}
