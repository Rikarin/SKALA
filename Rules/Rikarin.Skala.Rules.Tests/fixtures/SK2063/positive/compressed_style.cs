// ⚠ No space before the `=` and the asymmetry is still there: `=-` written as one token, the
// operand pushed away. The first draft declined this and had no argument for doing so.
class C {
    void M() {
        var remaining=10;
        remaining=- 1;
        Use(remaining);
    }

    static void Use(int value) { }
}
