class C {
    void M(int a, int b) {
        var value = 0;
        value = -a + b;
        Use(value);
    }

    static void Use(int value) { }
}
