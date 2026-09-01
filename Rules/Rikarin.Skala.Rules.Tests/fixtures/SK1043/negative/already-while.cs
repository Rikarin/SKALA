public sealed class Plain {
    static bool Advance() => false;

    public static void Drain() {
        while (Advance()) { }
    }
}
