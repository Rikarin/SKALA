// ⚠ The fixture a sabotage said was missing. `x = - 1` has spaces on both sides of the sign, so
// nothing groups the `=` and the `-` into one token and there is no `-=` to misread. Without this
// file, removing the adjacency test turned nothing red — a guard nothing could reach.
class C {
    void M() {
        var remaining = 10;
        remaining = - 1;
        Use(remaining);
    }

    static void Use(int value) { }
}
