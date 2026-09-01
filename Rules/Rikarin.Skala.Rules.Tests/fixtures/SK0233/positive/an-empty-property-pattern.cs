public static class Typed {
    public static bool IsText(object? value) => value is string { };
}
