public sealed class InsideElse {
    static void Emit() { }

    // The inner `if` is the body of an `else`, not of an `if`.
    public static void Handle(bool a, bool b) {
        if (a) {
            Emit();
        } else {
            if (b) {
                Emit();
            }
        }
    }
}
