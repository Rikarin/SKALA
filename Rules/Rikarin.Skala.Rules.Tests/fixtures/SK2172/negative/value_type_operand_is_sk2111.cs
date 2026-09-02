// ⚠ SK2111 owns a `!` that is inert because its operand is a non-nullable value type. Declining it
// here is what makes the two rules disjoint by construction rather than by filter — this fixture
// satisfies SK2111's shape and this rule's, and only SK2111 reports it.
class C {
    void M(int count) {
        if (count! is int) {
            Handle();
        }
    }

    static void Handle() { }
}
