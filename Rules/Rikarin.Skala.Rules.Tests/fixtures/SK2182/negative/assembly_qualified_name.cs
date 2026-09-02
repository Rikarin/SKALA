sealed class Order { }

static class Route {
    // A version, a culture and a public key token: a statement about which *build* of a type this
    // is, which `typeof(T)` cannot make.
    public static bool IsPinnedOrder(object entity) =>
        entity.GetType().AssemblyQualifiedName == "Order, fixtures, Version=1.0.0.0";
}
