public sealed class InnerElse {
    static void Emit() { }

    static void Fallback() { }

    // `if (a && b) Emit(); else Fallback();` runs `Fallback` when `a` is false, and the original
    // does not.
    public static void Handle(bool a, bool b) {
        if (a) {
            if (b) {
                Emit();
            } else {
                Fallback();
            }
        }
    }
}
