class C {
    public static string Name(int value, bool ready) =>
        value switch {
            1 => "one",
            _ when ready => "one",
            _ => "other"
        };
}
