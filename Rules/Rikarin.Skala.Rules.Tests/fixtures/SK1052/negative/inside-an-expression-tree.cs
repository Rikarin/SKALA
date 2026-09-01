using System;
using System.Linq.Expressions;

public sealed class Element;

public sealed class Document {
    public Element? Root;
}

// `?.` is CS8072 inside an expression tree.
public sealed class Reader {
    public Expression<Func<Document?, Element?>> RootOf() => document => document != null ? document.Root : null;
}
