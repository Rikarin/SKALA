class Root {
    public const int Maximum = 10;
}

sealed class Leaf : Root { }

static class Read {
    public static int Value() => Leaf.Maximum;
}
