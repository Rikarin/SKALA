class C {
    public static string Name(object value) =>
        value switch {
            string text => text,
            _ => "other"
        };
}
