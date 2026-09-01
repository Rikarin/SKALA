public sealed class Chain {
    int total;

    // `total + a + b` parses as `(total + a) + b`, so the operator's left operand is `total + a`
    // and not the target. Nothing here is rewritten.
    public void Add(int a, int b) {
        total = total + a + b;
    }

    public int Value => total;
}
