// ⚠ `{ }` matches a non-null `Nullable<T>` and a boxed struct as well as a reference, and `is null`
// on a `T?` is `!HasValue` — the rewrite preserves all of it, and these are the spellings that
// already say so.
class C {
    bool Nullable(int? value) => value is null;

    bool Unconstrained<T>(T value) => value is null;

    bool Boxed(object? value) => value is null;
}
