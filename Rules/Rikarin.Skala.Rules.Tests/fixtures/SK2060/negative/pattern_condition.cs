class C {
    void M(object? value) {
        if (value is string text) {
            Use(text);
        }
    }

    static void Use(string value) { }
}
