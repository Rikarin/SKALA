class Root {
    public static int Total() => 0;
}

sealed class Leaf : Root { }

static class Read {
    public static int Value() => Leaf.Total();
}
