public sealed class Guarded {
    static void Emit(int value) { }

    public static void Handle(int value, bool enabled) {
        if (enabled)
            if (value > 0)
                Emit(value);
    }
}
