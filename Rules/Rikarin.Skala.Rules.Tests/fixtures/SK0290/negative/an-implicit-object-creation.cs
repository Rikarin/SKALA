public static class ImplicitCreation {
    // ⚠ `new(…)` is an `ImplicitObjectCreationExpression`, a different syntax kind, and it does not
    // even mean the same thing: under a `T?` target the constructor bound is `T`'s, so
    // `int? wrapped = new(value);` is `CS1729` — measured — and `new()` leaves `wrapped` holding
    // zero rather than null. Nothing is written here to be redundant.
    public static int? Go() {
        int? wrapped = new();
        return wrapped;
    }
}
