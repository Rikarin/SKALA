class Root {
    public static int Limit { get; set; }
}

sealed class Leaf : Root { }

static class Read {
    public static int Value() => Leaf.Limit;
}
