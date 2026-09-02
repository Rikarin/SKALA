class C {
    void M(int count) {
        var line = $"{count}\x1F rows";
        Use(line);
    }

    static void Use(string value) { }
}
