class C {
    public static string Name(int value, bool ready) =>
        value switch {
            1 when ready => "other",
            _ => "other"
        };
}
