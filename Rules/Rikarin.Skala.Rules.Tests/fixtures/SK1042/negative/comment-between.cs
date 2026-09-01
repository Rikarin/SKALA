public sealed class Annotated {
    static void Emit() { }

    public static void Handle(bool a, bool b) {
        if (a) {
            // Only once the cache has been primed, which `a` implies.
            if (b) {
                Emit();
            }
        }
    }
}
