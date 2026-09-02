class C {
    public static string Name(int value) =>
        value switch {
            1 => "one",
            2 => "two",
            _ => "other"
        };
}
