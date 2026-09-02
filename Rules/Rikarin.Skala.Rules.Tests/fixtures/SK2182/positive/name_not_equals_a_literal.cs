sealed class Invoice { }

static class Route {
    public static bool IsNotInvoice(object entity) => entity.GetType().Name != "Invoice";
}
