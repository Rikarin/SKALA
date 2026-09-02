// The two spellings this rule exists to tell apart, both written so they cannot be misread.
class C {
    void M(object? value) {
        if (!(value is string)) {
            Handle();
        }

        if (value is not string) {
            Handle();
        }
    }

    static void Handle() { }
}
