class C {
    void M(object? value) {
        if (value! is string text) {
            Handle(text);
        }
    }

    static void Handle(string value) { }
}
