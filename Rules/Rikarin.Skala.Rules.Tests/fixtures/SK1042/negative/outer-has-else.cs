public sealed class OuterElse {
    static void Emit() { }

    static void Fallback() { }

    // Merging would lose the branch: when `a` is false the original runs `Fallback`.
    public static void Handle(bool a, bool b) {
        if (a) {
            if (b) {
                Emit();
            }
        } else {
            Fallback();
        }
    }
}
