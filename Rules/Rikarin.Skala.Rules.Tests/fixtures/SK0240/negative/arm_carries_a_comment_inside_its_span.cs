class C {
    public static string Name(int value) =>
        value switch {
            1 /* reserved by the protocol */ => "other",
            _ => "other"
        };
}
