public static class Asserted {
    // `(string)maybe` is typed `string` where its operand is typed `string?`, so the cast is what
    // tells the flow analysis what the author knows.
    public static string Force(string? maybe) {
        var text = (string)maybe;
        return text;
    }
}
