class C {
    void M(object? value) {
        if (value is string) {
            Handle(value);
        }

        if (value is not string) {
            Handle(value);
        }
    }

    static void Handle(object? value) { }
}
