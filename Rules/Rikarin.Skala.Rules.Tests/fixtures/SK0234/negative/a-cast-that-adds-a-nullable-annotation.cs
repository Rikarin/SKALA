public static class Widened {
    // `var copy = (string?)text;` is a `string?` and `var copy = text;` is a `string`. The written
    // annotation is the difference, and it cannot be read off the target symbol.
    public static string? Loosen(string text) {
        var copy = (string?)text;
        return copy;
    }
}
