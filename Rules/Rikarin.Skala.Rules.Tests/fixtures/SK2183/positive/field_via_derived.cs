class Root {
    public static int Count;
}

sealed class Leaf : Root { }

static class Read {
    public static int Value() => Leaf.Count;
}
