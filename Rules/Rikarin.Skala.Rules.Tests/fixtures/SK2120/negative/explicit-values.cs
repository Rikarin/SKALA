// ⚠ The exclusion that keeps this rule disjoint from CA1027. Every value is written down, and a
// declaration laid out as powers of two is a bit set whose author forgot the attribute — which is
// CA1027's finding, on the declaration, not this rule's finding on the use.
enum Access {
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}

sealed class Gate {
    public Access Combine(Access left, Access right) => left | right;

    public bool Allows(Access held, Access wanted) => (held & wanted) != 0;

    public Access Invert(Access held) => ~held;
}
