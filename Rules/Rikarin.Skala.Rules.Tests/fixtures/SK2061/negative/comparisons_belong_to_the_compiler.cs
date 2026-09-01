// ⚠ Measured rather than assumed: `csc` reports every one of these as CS1718, on by default —
// simple names, `this.` paths, member access to a field, and a static field. The rule's first draft
// examined the comparison operators and the measurement disposed of that half entirely. (The
// fixture carries CS1718 warnings, which is exactly the point.)
class Box {
    public int v;
    public static int Which;
}

class C {
    int g;

    bool A(int q) => q == q;

    bool B(int q) => q < q;

    bool D() => this.g == this.g;

    bool E(Box b) => b.v != b.v;

    bool G() => Box.Which >= Box.Which;

    bool H(string s) => s == s;
}
