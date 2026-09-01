public static class Patterns {
    public static bool IsText(object candidate) => candidate is string text && text.Length > 0;

    public static bool IsNumber(object candidate) => candidate is int number;
}
