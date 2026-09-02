class C {
    string M(object? value) {
        switch (value) {
            case not { }:
                return "missing";
            default:
                return "present";
        }
    }
}
