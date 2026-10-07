// A positional pattern names no element; a property pattern and a `with` initializer write `Item1` as a
// bare member name, not as a member access. None of them is this rule's shape.
public static class Shapes {
    public static bool Check((int Count, string Name) t) =>
        t is (1, _) || t is { Item1: 2 } || (t with { Item1 = 3 }).Count == 3;
}
