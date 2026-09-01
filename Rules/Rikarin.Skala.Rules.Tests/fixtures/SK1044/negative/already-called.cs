public sealed class Done {
    public static bool IsBlank(string? value) => string.IsNullOrEmpty(value);

    public static bool HasValue(string? value) => !string.IsNullOrEmpty(value);
}
