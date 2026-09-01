public sealed class Element;

// `!=` here is whatever the type says it is; `?.` is always the reference test.
public sealed class Document {
    public Element? Root;

    public static bool operator ==(Document? left, Document? right) => true;

    public static bool operator !=(Document? left, Document? right) => false;

    public override bool Equals(object? other) => other is Document;

    public override int GetHashCode() => 0;
}

public sealed class Reader {
    public Element? RootOf(Document? document) => document != null ? document.Root : null;
}
