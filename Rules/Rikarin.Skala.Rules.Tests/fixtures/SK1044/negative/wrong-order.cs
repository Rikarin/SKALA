public sealed class Ordering {
    // This dereferences before it tests. `string.IsNullOrEmpty` would not throw where this does,
    // so the rewrite would hide a bug rather than express it.
    public static bool IsBlank(string value) => value.Length == 0 || value == null;
}
