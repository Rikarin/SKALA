public sealed class Precedence {
    static void Emit() { }

    // `a || b` merged with `c` is `(a || b) && c`. Flattened without the parentheses it would be
    // `a || (b && c)`, which is a different predicate.
    public static void Handle(bool a, bool b, bool c) {
        if (a || b) {
            if (c) {
                Emit();
            }
        }
    }
}
