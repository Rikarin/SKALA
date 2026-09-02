class C {
    public static string Name(int value, string fallback) =>
        value switch {
            1 => fallback,
            _ => fallback
        };
}
