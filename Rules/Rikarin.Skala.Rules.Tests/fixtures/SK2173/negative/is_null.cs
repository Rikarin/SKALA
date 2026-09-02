// The repair, and every spelling of a null test that already says what it means.
class C {
    bool Missing(object? result) => result is null;

    bool Present(object? result) => result is not null;

    string Switched(object? value) =>
        value switch {
            null => "missing",
            _ => "present"
        };
}
