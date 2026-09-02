using System;

sealed class Order { }

static class Route {
    // The subject is a type, not an instance, so there is no `GetType() == typeof(...)` rewrite.
    public static bool IsOrder(Type contract) => contract.Name == "Order";
}
