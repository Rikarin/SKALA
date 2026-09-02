using System;

sealed class Shipment { }

static class Route {
    public static bool IsShipment(object entity) =>
        entity.GetType().Name.Equals("Shipment", StringComparison.Ordinal);
}
