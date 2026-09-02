sealed class Box<T> {
    public T? Value { get; set; }
}

static class Route {
    // `Box<T>`'s metadata name carries the arity, so `Box` does not resolve to it and a `typeof`
    // would need type arguments this rule cannot invent.
    public static bool IsBox(object entity) => entity.GetType().Name == "Box";
}
