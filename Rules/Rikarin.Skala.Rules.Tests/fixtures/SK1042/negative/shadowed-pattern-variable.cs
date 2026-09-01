public sealed class Shadowed {
    static void Emit(string value) { }

    // ⚠ The two `text`s are cousins today: one lives in the first `if`'s block, and the pattern
    // variable is scoped to the second `if`'s block. Merging lifts the pattern into this method's
    // block, where it encloses the first — CS0136. The rewrite would not compile, so the chain is
    // declined.
    public static void Handle(object candidate, bool enabled, bool other) {
        if (other) {
            string text = "sibling";
            Emit(text);
        }

        if (enabled) {
            if (candidate is string text) {
                Emit(text);
            }
        }
    }
}
