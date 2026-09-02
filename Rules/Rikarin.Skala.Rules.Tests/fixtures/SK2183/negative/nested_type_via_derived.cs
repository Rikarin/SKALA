class Root {
    public sealed class Nested { }
}

sealed class Leaf : Root { }

static class Read {
    // A nested *type* reached through a derived type is a different language rule.
    public static Root.Nested Value() => new Leaf.Nested();
}
