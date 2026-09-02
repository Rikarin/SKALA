class C {
    string M(object? value) =>
        value switch {
            not { } => "missing",
            _ => "present"
        };
}
