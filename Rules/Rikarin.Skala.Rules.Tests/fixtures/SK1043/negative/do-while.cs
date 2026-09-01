public sealed class AtLeastOnce {
    static bool Advance() => false;

    public static void Drain() {
        do { } while (Advance());
    }
}
