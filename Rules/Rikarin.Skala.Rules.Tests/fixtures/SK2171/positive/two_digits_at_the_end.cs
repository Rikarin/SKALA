class C {
    void M() {
        var separator = "field\x1F";
        Use(separator);
    }

    static void Use(string value) { }
}
