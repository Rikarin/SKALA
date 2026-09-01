public sealed class Draining {
    static bool Advance() => false;

    static void Consume() { }

    public static void Drain() {
        for (; Advance();) {
            Consume();
        }
    }
}
