class C {
    void M(int delta) {
        var total = 0;
        total =+ delta;
        Use(total);
    }

    static void Use(int value) { }
}
