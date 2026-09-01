// ⚠ `x =-1` is how somebody writing a negative literal in a hurry spells it: the operand is not
// pushed away from the sign, so nothing groups the two operator characters together.
class C {
    void M() {
        var remaining = 10;
        remaining =-1;
        Use(remaining);
    }

    static void Use(int value) { }
}
