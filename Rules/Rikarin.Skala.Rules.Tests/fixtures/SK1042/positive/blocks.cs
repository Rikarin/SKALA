public sealed class Order {
    public bool IsPaid { get; init; }
}

public sealed class Shipping {
    static void Ship(Order order) { }

    public static void Handle(Order? order) {
        if (order != null) {
            if (order.IsPaid) {
                Ship(order);
            }
        }
    }
}
