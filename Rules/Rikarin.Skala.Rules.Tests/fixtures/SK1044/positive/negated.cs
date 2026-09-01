public sealed class Emitting {
    static void Emit(string value) { }

    // `[NotNullWhen(false)]` on IsNullOrEmpty tells the null analysis what the pair told it, so
    // `Emit(value)` stays warning-free after the rewrite.
    public static void Handle(string? value) {
        if (value != null && value.Length > 0) {
            Emit(value);
        }
    }
}
