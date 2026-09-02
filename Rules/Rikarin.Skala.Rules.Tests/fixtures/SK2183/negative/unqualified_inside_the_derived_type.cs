class Root {
    protected static int Count;
}

sealed class Leaf : Root {
    // No written qualifier, so no member access and nothing for the rule to look at.
    public int Value() => Count;
}
