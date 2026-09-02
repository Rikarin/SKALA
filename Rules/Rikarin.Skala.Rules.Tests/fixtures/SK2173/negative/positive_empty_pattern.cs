// `is { }` is not this rule: it reads as "has something" and means it. Only the negation inverts.
class C {
    bool M(object? result) => result is { };
}
