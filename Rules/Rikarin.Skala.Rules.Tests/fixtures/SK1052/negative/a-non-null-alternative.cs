public sealed class Element;

public sealed class Document {
    public Element? Root;
}

// The alternative is a value rather than null, so this is a `??` at best and never a `?.`.
public sealed class Reader {
    static readonly Element Fallback = new();

    public Element? RootOf(Document? document) => document != null ? document.Root : Fallback;
}
