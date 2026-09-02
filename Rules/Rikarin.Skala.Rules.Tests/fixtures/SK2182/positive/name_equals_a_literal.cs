sealed class Order { }

static class Route {
    public static bool IsOrder(object entity) => entity.GetType().Name == "Order";
}
