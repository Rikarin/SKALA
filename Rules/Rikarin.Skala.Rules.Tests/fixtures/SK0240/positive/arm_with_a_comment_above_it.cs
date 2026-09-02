class C {
    public static string Name(int value) =>
        value switch {
            // the protocol reserves 1
            1 => "other",
            _ => "other"
        };
}
