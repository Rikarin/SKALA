// `?.`, `??`, `??=` and a nullable `T?` all carry a question mark and none of them is a conditional
// expression. Nesting them costs nothing, because none of them has two branches to choose between.
namespace Fixtures;

class Names {
    public static int Length(string? first, string? second, string? third) =>
        (first?.Trim() ?? second?.Trim() ?? third ?? string.Empty).Length;

    public static string? Fallback(string? value) {
        value ??= "none";
        return value?.ToUpperInvariant();
    }
}
