public sealed class Whitespace {
    // ⚠ A different predicate, not a longer spelling of the same one: this is true for `" "` and
    // `string.IsNullOrEmpty(" ")` is false. That rewrite would silently change behaviour, which is
    // why the receiver must be a chain of plain names and `Trim()` is not one.
    public static bool IsBlank(string? value) => value == null || value.Trim().Length == 0;
}
