public sealed class Single {
    static void Emit() { }

    public static void Handle(bool a, bool b) {
        if (a && b) {
            Emit();
        }
    }
}
