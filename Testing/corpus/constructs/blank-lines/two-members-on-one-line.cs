class C {
    // ⚠ The oracle gives every member its own line; Skala does not yet, and the shape is here for
    // the idempotency property rather than for the fidelity number. A member that shares a line has
    // no stable notion of "single line", which is what the blank-line keys branch on.
    public int A => 1;    public int B => 2;
    public int C => 3;
}
