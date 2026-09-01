public sealed class Element {
    public Element? Parent;
}

public sealed class Document {
    public Element Root = new();
}

// The inverted spelling and a two-step chain: `x == null ? null : x.Y.Z`.
public sealed class Reader {
    public Element? GrandParent(Document? document) => document == null ? null : document.Root.Parent;
}
