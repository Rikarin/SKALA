public static class Guarded {
    public static void Require(string value) {
        // The null-check idiom. The result is discarded, so the dereference is the call — and
        // `value;` is not an expression statement.
        value.ToString();
    }
}
