public static class Sized {
    public static bool NonEmpty(string? text) => text is { Length: > 0 };
}
