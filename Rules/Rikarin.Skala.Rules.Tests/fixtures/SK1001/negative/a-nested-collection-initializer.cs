using System.Collections.Generic;

// `Tags = { "a" }` adds to the collection the property already returns; it neither creates one nor
// assigns one. There is no `new` here to rewrite, and reading it as an assignment would turn an
// `Add` into a replacement.
public sealed class Product {
    public List<string> Tags { get; } = [];
}

public sealed class Catalogue {
    public Product One() => new Product { Tags = { "a", "b" } };
}
