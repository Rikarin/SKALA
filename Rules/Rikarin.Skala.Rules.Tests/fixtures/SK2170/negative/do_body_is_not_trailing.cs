// A `do` body is not trailing — the `while` follows it — so nothing below the statement can be
// misread as belonging to it.
class C {
    void M(int limit) {
        var i = 0;
        do
            i++;
        while (i < limit);
            Use(i);
    }

    static void Use(int value) { }
}
