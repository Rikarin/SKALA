// A variable declarator's `=` is not an assignment expression and is not examined.
class C {
    void M() {
        int remaining =- 1;
        Use(remaining);
    }

    static void Use(int value) { }
}
