// The target is a plain local, not a member access through the guarded name, so there is no
// receiver for a `?` to attach to and nothing about the guard is redundant.
public sealed class Order;

public sealed class Desk {
    public Order? Latest;

    public void Assign(Order? order) {
        Order? held = null;
        if (order is not null) {
            held = order;
        }

        Latest = held;
    }
}
