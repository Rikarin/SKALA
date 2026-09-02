class C {
    void M() {
        var cyrillic = "\x41B";
        Use(cyrillic);
    }

    static void Use(string value) { }
}
