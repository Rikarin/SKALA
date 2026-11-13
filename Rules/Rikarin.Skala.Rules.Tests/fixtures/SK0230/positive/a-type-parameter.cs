public static class Factory {
    // A `new()`-constrained type parameter is a creation like any other, and grows its argument list.
    public static T Create<T>() where T : new() => new T { };
}
