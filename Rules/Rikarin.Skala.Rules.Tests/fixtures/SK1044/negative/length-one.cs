public sealed class TooShort {
    public static bool IsTooShort(string? value) => value == null || value.Length == 1;
}
