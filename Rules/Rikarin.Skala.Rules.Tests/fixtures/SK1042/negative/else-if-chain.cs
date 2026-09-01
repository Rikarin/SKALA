public sealed class Chain {
    static void First() { }

    static void Second() { }

    // An `else if` is a sibling branch, not a nesting.
    public static void Handle(bool a, bool b) {
        if (a) {
            First();
        } else if (b) {
            Second();
        }
    }
}
