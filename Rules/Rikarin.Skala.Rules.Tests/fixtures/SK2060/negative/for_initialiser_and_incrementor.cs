// The initialiser and the incrementor are assignments by design and are not conditions.
class C {
    void M(int n) {
        var i = 0;
        for (i = 0; i < n; i = i + 1) {
            Step(i);
        }
    }

    static void Step(int i) { }
}
