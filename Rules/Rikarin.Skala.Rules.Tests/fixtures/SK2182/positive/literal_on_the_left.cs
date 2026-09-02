sealed class Receipt { }

static class Route {
    public static bool IsReceipt(object entity) => "Receipt" == entity.GetType().Name;
}
