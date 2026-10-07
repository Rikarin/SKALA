// A tuple of type parameters still has names; only the element types are open.
public static class Pairs {
    public static T First<T>((T Head, T Tail) pair) => pair.Item1;
}
