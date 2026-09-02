sealed class Order { }

static class Route {
    // ⚠ Deliberately contrived, and the contrivance is the point: the literal resolves, so the
    // *only* thing declining this is the property filter. The realistic sibling fixture —
    // `assembly_qualified_name` — carries a version and a token, which no simple name resolves to,
    // so it passes for the resolution reason and proves nothing about the filter. Sabotaging the
    // filter left it green, which is how this file came to exist.
    public static bool IsPinnedOrder(object entity) => entity.GetType().AssemblyQualifiedName == "Order";
}
