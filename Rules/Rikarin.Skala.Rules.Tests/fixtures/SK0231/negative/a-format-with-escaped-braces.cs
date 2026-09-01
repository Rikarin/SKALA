public static class Escaped {
    // `string.Format("{{0}}")` returns `{0}`; the literal returns `{{0}}`.
    public static string Token() => string.Format("{{0}}");
}
