public sealed class Element {
    public Element? Parent;
}

public sealed class Document {
    public Element? Root;
}

public sealed class Reader {
    public Element? RootOf(Document? document) => document != null ? document.Root : null;
}
