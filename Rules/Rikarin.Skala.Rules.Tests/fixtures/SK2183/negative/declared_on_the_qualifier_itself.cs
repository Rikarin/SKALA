class Root {
    public static int Count;
}

sealed class Leaf : Root {
    public static new int Count;
}

static class Read {
    public static int Value() => Leaf.Count;
}
