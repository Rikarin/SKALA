class C {
    public static string Name(int value, bool ready) =>
        value switch {
            1 => "same",
            _ when ready => "same"
        };
}
