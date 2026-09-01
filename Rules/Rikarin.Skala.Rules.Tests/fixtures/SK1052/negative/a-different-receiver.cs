public sealed class Element;

public sealed class Document {
    public Element? Root;
}

// The guard and the access are about different objects, which is a bug the rule must not tidy away.
public sealed class Reader {
    public Element? RootOf(Document? first, Document second) => first != null ? second.Root : null;
}
