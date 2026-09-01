public static class Loose {
    // Deleting the call would leave a `string?` where a `string` stood, which is CS8600.
    public static string Describe(string? value) {
        string text = value.ToString();
        return text;
    }
}
