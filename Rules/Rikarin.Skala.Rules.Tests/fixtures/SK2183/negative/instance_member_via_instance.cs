class Root {
    public int Count;
}

sealed class Leaf : Root { }

static class Read {
    public static int Value(Leaf leaf) => leaf.Count;
}
