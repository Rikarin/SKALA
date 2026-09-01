class C {
    void M(bool a, bool b) {
        a = b;
        b = a;
        var c = a = b;
        Use(c);
    }

    static void Use(bool value) { }
}
