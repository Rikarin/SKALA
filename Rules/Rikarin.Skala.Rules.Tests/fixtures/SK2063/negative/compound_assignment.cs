class C {
    void M() {
        var remaining = 10;
        remaining -= 1;
        remaining += 2;
        Use(remaining);
    }

    static void Use(int value) { }
}
