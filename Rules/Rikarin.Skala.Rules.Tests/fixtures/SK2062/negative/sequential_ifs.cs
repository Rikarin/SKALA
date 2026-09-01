// ⚠ The rule's main exclusion. These are two separate statements and the first body is exactly the
// thing that changes the answer, so a repeat here needs the bodies read before it means anything.
class C {
    bool dirty;

    void M() {
        if (dirty) {
            Flush();
        }

        if (dirty) {
            Report();
        }
    }

    void Flush() => dirty = false;

    static void Report() { }
}
