// The parentheses say which operand the `!` binds to, which is the entire thing the reader of the
// bare form cannot see.
class C {
    void M(object? value) {
        if ((value!) is string) {
            Handle(value);
        }
    }

    static void Handle(object? value) { }
}
