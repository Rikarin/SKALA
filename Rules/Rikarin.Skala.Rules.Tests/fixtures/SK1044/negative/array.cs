public sealed class Arrays {
    // `Length` is not a string's, and there is no `IsNullOrEmpty` for an array.
    public static bool IsEmpty(int[]? values) => values == null || values.Length == 0;
}
