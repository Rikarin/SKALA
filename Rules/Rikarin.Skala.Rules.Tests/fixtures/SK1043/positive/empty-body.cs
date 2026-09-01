public sealed class Spinning {
    static bool Advance() => false;

    public static void Exhaust() {
        for (; Advance();) { }
    }
}
