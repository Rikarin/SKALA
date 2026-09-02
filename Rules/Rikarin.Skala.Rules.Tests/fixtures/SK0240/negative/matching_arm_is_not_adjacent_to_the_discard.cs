class C {
    public static string Name(int value) =>
        value switch {
            1 => "other",
            2 => "two",
            _ => "other"
        };
}
