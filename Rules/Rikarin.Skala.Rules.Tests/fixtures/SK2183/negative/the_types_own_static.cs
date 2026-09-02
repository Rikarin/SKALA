class Root {
    public static int Count;
}

sealed class Leaf : Root {
    public static int Own;
}

static class Read {
    public static int Value() => Leaf.Own;
}
