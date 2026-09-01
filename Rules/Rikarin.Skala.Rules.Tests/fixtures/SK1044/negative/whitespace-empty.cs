public sealed class WhitespaceLiteral {
    // The same `IsNullOrWhiteSpace` predicate written against the literal instead of `Length`.
    public static bool IsBlank(string? value) => value is null || value.Trim() == "";
}
