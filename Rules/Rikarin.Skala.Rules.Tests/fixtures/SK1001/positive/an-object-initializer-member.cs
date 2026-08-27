// A member assignment inside an object initializer is an assignment like any other, and the type is
// written at the member's declaration. C# 12 allows a collection expression there and it takes the
// member's type as its target, so the rewrite is the same proof as everywhere else in this rule.
public sealed class Product {
    public string Name { get; set; } = string.Empty;

    public string[] Sizes { get; set; } = [];
}

public sealed class Catalogue {
    public Product One() => new Product { Name = "Widget", Sizes = new[] { "Small", "Medium", "Large" } };
}
