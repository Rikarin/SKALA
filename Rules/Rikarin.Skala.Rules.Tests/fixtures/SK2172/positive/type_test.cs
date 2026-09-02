class C {
    void M(object? value) {
        if (value! is string) {
            Handle(value);
        }
    }

    static void Handle(object? value) { }
}
