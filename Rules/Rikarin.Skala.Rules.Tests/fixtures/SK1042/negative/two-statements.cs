public sealed class NotSole {
    static void Emit() { }

    static void Prepare() { }

    // The inner `if` is not the outer body; there is a statement beside it.
    public static void Handle(bool a, bool b) {
        if (a) {
            Prepare();
            if (b) {
                Emit();
            }
        }
    }
}
