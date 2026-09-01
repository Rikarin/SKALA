// Trivia between the `=` and the sign, so the two characters are not written as one token.
class C {
    void M() {
        var remaining = 10;
        remaining =
            -1;
        Use(remaining);
    }

    static void Use(int value) { }
}
