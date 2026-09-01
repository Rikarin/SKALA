public sealed class PresentAndEmpty {
    // "present and empty" is not "null or empty", and there is no BCL call for it.
    public static bool IsPresentAndEmpty(string? value) => value != null && value.Length == 0;
}
