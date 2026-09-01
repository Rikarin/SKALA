public sealed class Deep {
    static void Emit() { }

    public static void Handle(bool a, bool b, bool c) {
        if (a) {
            if (b) {
                if (c) {
                    Emit();
                }
            }
        }
    }
}
