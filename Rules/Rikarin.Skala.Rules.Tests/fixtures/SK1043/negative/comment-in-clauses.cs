public sealed class Annotated {
    static bool Advance() => false;

    // The fix replaces the whole header, so a comment written in an empty clause is text it would
    // silently take with it.
    public static void Drain() {
        for (/* the reader owns the cursor */; Advance();) { }
    }
}
