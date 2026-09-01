public sealed class Present {
    public static bool HasValue(string? value) => value is not null && value != "";
}
