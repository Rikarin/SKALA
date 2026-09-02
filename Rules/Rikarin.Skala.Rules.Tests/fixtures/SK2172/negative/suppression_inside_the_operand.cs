// Each of these suppresses a real warning on something *inside* the operand, so the `!` is not inert
// and the two tokens are not adjacent.
class C {
    void M(string[]? items, string? text) {
        if (items![0] is string) {
            Handle();
        }

        if (text!.Trim() is { Length: > 0 }) {
            Handle();
        }
    }

    static void Handle() { }
}
