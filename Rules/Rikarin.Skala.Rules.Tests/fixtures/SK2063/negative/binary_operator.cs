// The right side is a binary expression, not a unary one. `a -1` is subtraction however it is
// spaced, and this rule examines only the unary case.
class C {
    void M(int a) {
        var remaining = a - 1;
        var other = a -1;
        Use(remaining + other);
    }

    static void Use(int value) { }
}
