// `~` has no compound-assignment reading, so `=~` misleads nobody.
class C {
    void M(int bits) {
        var mask = 0;
        mask =~ bits;
        Use(mask);
    }

    static void Use(int value) { }
}
