public static class Braces {
    // Removing the `$` would stop the doubled braces being an escape.
    public static string Literal() => $"a {{token}} placeholder";
}
