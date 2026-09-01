// A compressed style. There is no trivia anywhere, so there is no asymmetry to read.
class C {
    void M() {
        var remaining=10;
        remaining=-1;
        Use(remaining);
    }

    static void Use(int value) { }
}
