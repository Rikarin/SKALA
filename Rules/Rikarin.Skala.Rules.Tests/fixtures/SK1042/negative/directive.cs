public sealed class Conditional {
    static void Emit() { }

    // The active branch is mergeable on its own. Merging it would move a brace the other symbol
    // set owns, so the whole chain is declined.
    public static void Handle(bool a, bool b, bool c) {
        if (a) {
#if DEBUG
            if (b) {
                Emit();
            }
#else
            if (c) {
                Emit();
            }
#endif
        }
    }
}
