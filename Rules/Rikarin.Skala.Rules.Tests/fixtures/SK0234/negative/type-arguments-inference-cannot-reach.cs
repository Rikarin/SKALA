public static class Uninferable {
    static T Create<T>() where T : new() => new T();

    // Nothing to infer from: the type argument is the only thing choosing T.
    public static object Make() => Create<object>();
}
