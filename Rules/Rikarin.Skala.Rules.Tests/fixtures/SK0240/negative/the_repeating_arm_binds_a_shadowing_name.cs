class C {
    string text = "field";

    public string Name(object value) =>
        value switch {
            string text => text,
            _ => text
        };
}
