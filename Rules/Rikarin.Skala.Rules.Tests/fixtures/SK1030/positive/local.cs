public sealed class Holder {
    public static string Pick(string? candidate) {
        var value = candidate;
        value = value ?? "fallback";
        return value;
    }
}
