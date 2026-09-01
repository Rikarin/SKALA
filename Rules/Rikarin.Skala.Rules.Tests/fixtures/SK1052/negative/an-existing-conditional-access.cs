public sealed class Element;

public sealed class Document {
    public Element? Root;
}

// Appending the suffix to the receiver would splice `x??.Root`.
public sealed class Reader {
    public Element? RootOf(Document? document) => document != null ? document?.Root : null;
}
