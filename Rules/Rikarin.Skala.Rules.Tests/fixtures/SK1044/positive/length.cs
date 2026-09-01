public sealed class Naming {
    public static string Display(string? name) {
        if (name == null || name.Length == 0) {
            return "anonymous";
        }

        return name.ToUpperInvariant();
    }
}
