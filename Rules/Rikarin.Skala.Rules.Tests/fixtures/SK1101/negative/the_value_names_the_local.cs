public sealed class Parsing {
    static int Parse(string text, out int parsed) {
        parsed = text.Length;
        return parsed;
    }

    // ⚠ `int parsed; parsed = Parse(text, out parsed);` is legal and the joined form is not — a
    // local may not appear in its own initializer, `out` position included.
    public static int Count(string text) {
        int parsed;
        parsed = Parse(text, out parsed);
        return parsed;
    }
}
