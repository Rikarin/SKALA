// Another type's initializers run on first access to that type, not in this type's order.
static class Source {
    public static readonly int Value = 42;
}

static class Reader {
    public static readonly int Copy = Source.Value;
}
